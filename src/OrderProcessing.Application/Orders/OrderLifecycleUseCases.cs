using System.Text.Json;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;

namespace OrderProcessing.Application.Orders;

internal static class OrderLifecycleSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<Order> LoadOrder(IOrderStore orders, Guid id, CancellationToken cancellationToken)
    {
        return await orders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");
    }

    public static async Task<IReadOnlyDictionary<Guid, InventoryItem>> LoadInventoryForOrder(
        IInventoryStore inventory,
        Order order,
        CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(item => item.ProductId).Distinct().OrderBy(id => id).ToList();
        var items = await inventory.GetByProductIdsAsync(productIds, cancellationToken);
        var byProduct = items.ToDictionary(item => item.ProductId);

        foreach (var productId in productIds)
        {
            if (!byProduct.ContainsKey(productId))
            {
                throw new NotFoundException($"Inventory for product '{productId}' was not found.");
            }
        }

        return byProduct;
    }

    public static void ReserveStock(
        Order order,
        IReadOnlyDictionary<Guid, InventoryItem> inventory,
        IInventoryStore inventoryStore,
        DateTimeOffset utcNow)
    {
        foreach (var line in order.Items.OrderBy(item => item.ProductId))
        {
            var stock = inventory[line.ProductId];
            stock.Reserve(line.Quantity);
            inventoryStore.AddTransaction(InventoryTransaction.Create(
                line.ProductId,
                InventoryTransactionType.Reserve,
                line.Quantity,
                utcNow,
                order.Id,
                $"Reserved for order {order.OrderNumber.Value}"));
        }
    }

    public static void ReleaseStock(
        Order order,
        IReadOnlyDictionary<Guid, InventoryItem> inventory,
        IInventoryStore inventoryStore,
        DateTimeOffset utcNow,
        string reason)
    {
        foreach (var line in order.Items.OrderBy(item => item.ProductId))
        {
            var stock = inventory[line.ProductId];
            stock.Release(line.Quantity);
            inventoryStore.AddTransaction(InventoryTransaction.Create(
                line.ProductId,
                InventoryTransactionType.Release,
                line.Quantity,
                utcNow,
                order.Id,
                reason));
        }
    }

    public static void CommitSale(
        Order order,
        IReadOnlyDictionary<Guid, InventoryItem> inventory,
        IInventoryStore inventoryStore,
        DateTimeOffset utcNow)
    {
        foreach (var line in order.Items.OrderBy(item => item.ProductId))
        {
            var stock = inventory[line.ProductId];
            stock.CommitSale(line.Quantity);
            inventoryStore.AddTransaction(InventoryTransaction.Create(
                line.ProductId,
                InventoryTransactionType.Sale,
                line.Quantity,
                utcNow,
                order.Id,
                $"Sold for order {order.OrderNumber.Value}"));
        }
    }

    public static void AddAudit(
        IOrderAuditStore audits,
        Order order,
        OrderAuditEventType eventType,
        string message,
        Guid? actorId,
        DateTimeOffset utcNow,
        object? details = null)
    {
        audits.Add(OrderAuditEvent.Create(
            order.Id,
            eventType,
            utcNow,
            message,
            actorId,
            details is null ? null : JsonSerializer.Serialize(details, JsonOptions)));
    }

    public static void EnqueueOutbox(
        IOutboxStore outbox,
        ICorrelationContext correlation,
        string eventType,
        Order order,
        DateTimeOffset utcNow)
    {
        var payload = JsonSerializer.Serialize(new
        {
            orderId = order.Id,
            orderNumber = order.OrderNumber.Value,
            customerId = order.CustomerId,
            status = order.Status.ToString(),
            occurredAt = utcNow,
            correlationId = correlation.CorrelationId
        }, JsonOptions);

        outbox.Add(eventType, payload, utcNow, correlation.CorrelationId);
    }
}

public sealed class SubmitOrder
{
    private readonly IOrderStore _orders;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly OrderPricingCalculator _pricing;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public SubmitOrder(
        IOrderStore orders,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        OrderPricingCalculator pricing,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _pricing = pricing;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Submit(_pricing);
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Submitted, "Order submitted.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderSubmitted, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class ConfirmOrder
{
    private readonly IOrderStore _orders;
    private readonly IInventoryStore _inventory;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly OrderPricingCalculator _pricing;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public ConfirmOrder(
        IOrderStore orders,
        IInventoryStore inventory,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        OrderPricingCalculator pricing,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _inventory = inventory;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _pricing = pricing;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var stock = await OrderLifecycleSupport.LoadInventoryForOrder(_inventory, order, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Confirm(_pricing);
            OrderLifecycleSupport.ReserveStock(order, stock, _inventory, utcNow);
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }
        catch (InsufficientInventoryException exception)
        {
            throw new ConflictException(exception.Message);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Confirmed, "Order confirmed.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.InventoryReserved, "Inventory reserved.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderConfirmed, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class StartProcessingOrder
{
    private readonly IOrderStore _orders;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public StartProcessingOrder(
        IOrderStore orders,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.StartProcessing();
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.ProcessingStarted, "Order processing started.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderProcessingStarted, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class ShipOrder
{
    private readonly IOrderStore _orders;
    private readonly IInventoryStore _inventory;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public ShipOrder(
        IOrderStore orders,
        IInventoryStore inventory,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _inventory = inventory;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var stock = await OrderLifecycleSupport.LoadInventoryForOrder(_inventory, order, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Ship();
            OrderLifecycleSupport.CommitSale(order, stock, _inventory, utcNow);
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }
        catch (DomainException exception)
        {
            throw new ConflictException(exception.Message);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Shipped, "Order shipped.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderShipped, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class CompleteOrder
{
    private readonly IOrderStore _orders;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public CompleteOrder(
        IOrderStore orders,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Complete();
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Completed, "Order completed.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderCompleted, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class CancelOrder
{
    private readonly IOrderStore _orders;
    private readonly IInventoryStore _inventory;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public CancelOrder(
        IOrderStore orders,
        IInventoryStore inventory,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _inventory = inventory;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var previous = order.Status;
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Cancel();
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }

        if (previous == OrderStatus.Confirmed)
        {
            var stock = await OrderLifecycleSupport.LoadInventoryForOrder(_inventory, order, cancellationToken);
            OrderLifecycleSupport.ReleaseStock(order, stock, _inventory, utcNow, $"Released after cancel of {order.OrderNumber.Value}");
            OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.InventoryReleased, "Inventory released.", _currentUser.UserId, utcNow);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Cancelled, "Order cancelled.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderCancelled, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class FailOrder
{
    private readonly IOrderStore _orders;
    private readonly IInventoryStore _inventory;
    private readonly IOrderAuditStore _audits;
    private readonly IOutboxStore _outbox;
    private readonly IApplicationPersistence _persistence;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;

    public FailOrder(
        IOrderStore orders,
        IInventoryStore inventory,
        IOrderAuditStore audits,
        IOutboxStore outbox,
        IApplicationPersistence persistence,
        ICurrentUser currentUser,
        ICorrelationContext correlation)
    {
        _orders = orders;
        _inventory = inventory;
        _audits = audits;
        _outbox = outbox;
        _persistence = persistence;
        _currentUser = currentUser;
        _correlation = correlation;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await OrderLifecycleSupport.LoadOrder(_orders, id, cancellationToken);
        var previous = order.Status;
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            order.Fail();
        }
        catch (InvalidOrderTransitionException exception)
        {
            throw new ConflictException(exception.Message);
        }

        if (previous is OrderStatus.Confirmed or OrderStatus.Processing)
        {
            var stock = await OrderLifecycleSupport.LoadInventoryForOrder(_inventory, order, cancellationToken);
            OrderLifecycleSupport.ReleaseStock(order, stock, _inventory, utcNow, $"Released after fail of {order.OrderNumber.Value}");
            OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.InventoryReleased, "Inventory released.", _currentUser.UserId, utcNow);
        }

        OrderLifecycleSupport.AddAudit(_audits, order, OrderAuditEventType.Failed, "Order failed.", _currentUser.UserId, utcNow);
        OrderLifecycleSupport.EnqueueOutbox(_outbox, _correlation, OutboxEventTypes.OrderFailed, order, utcNow);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public static class OutboxEventTypes
{
    public const string OrderSubmitted = "order.submitted";
    public const string OrderConfirmed = "order.confirmed";
    public const string OrderProcessingStarted = "order.processing_started";
    public const string OrderShipped = "order.shipped";
    public const string OrderCompleted = "order.completed";
    public const string OrderCancelled = "order.cancelled";
    public const string OrderFailed = "order.failed";
}
