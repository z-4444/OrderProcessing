using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;
using OrderProcessing.Infrastructure.Messaging;

namespace OrderProcessing.Infrastructure.Persistence;

internal sealed class EfApplicationPersistence : IApplicationPersistence
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfApplicationPersistence(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The record was modified by another user. Reload and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }
}

internal sealed class EfCustomerStore : ICustomerStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfCustomerStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Email == normalized, cancellationToken);
    }

    public async Task<PagedResult<Customer>> ListAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var source = _dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            source = source.Where(customer =>
                customer.Name.ToLower().Contains(term) ||
                customer.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<CustomerStatus>(query.Status, ignoreCase: true, out var status))
        {
            source = source.Where(customer => customer.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Segment)
            && Enum.TryParse<CustomerSegment>(query.Segment, ignoreCase: true, out var segment))
        {
            source = source.Where(customer => customer.Segment == segment);
        }

        source = source.OrderBy(customer => customer.Name);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Customer>(items, query.Page, query.PageSize, total);
    }

    public void Add(Customer customer) => _dbContext.Customers.Add(customer);
}

internal sealed class EfProductStore : IProductStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfProductStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var skuValue = new Sku(sku);
        return _dbContext.Products.FirstOrDefaultAsync(product => product.Sku == skuValue, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => idList.Contains(product.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        var source = _dbContext.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            source = source.Where(product =>
                product.Name.ToLower().Contains(term) ||
                EF.Property<string>(product, "Sku").ToLower().Contains(term));
        }

        if (query.IsActive is not null)
        {
            source = source.Where(product => product.IsActive == query.IsActive);
        }

        source = source.OrderBy(product => product.Name);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Product>(items, query.Page, query.PageSize, total);
    }

    public void Add(Product product) => _dbContext.Products.Add(product);
}

internal sealed class EfOrderStore : IOrderStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfOrderStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .Include("_items")
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<PagedResult<Order>> ListAsync(OrderListQuery query, CancellationToken cancellationToken = default)
    {
        var source = _dbContext.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var status))
        {
            source = source.Where(order => order.Status == status);
        }

        if (query.CustomerId is not null)
        {
            source = source.Where(order => order.CustomerId == query.CustomerId);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            var term = query.OrderNumber.Trim().ToLowerInvariant();
            source = source.Where(order => EF.Property<string>(order, "OrderNumber").ToLower().Contains(term));
        }

        if (query.From is not null)
        {
            source = source.Where(order => order.CreatedAt >= query.From);
        }

        if (query.To is not null)
        {
            source = source.Where(order => order.CreatedAt <= query.To);
        }

        source = source.OrderByDescending(order => order.CreatedAt);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Order>(items, query.Page, query.PageSize, total);
    }

    public Task<int> CountCreatedOnUtcDateAsync(DateTime utcDate, CancellationToken cancellationToken = default)
    {
        var start = new DateTimeOffset(utcDate.Date, TimeSpan.Zero);
        var end = start.AddDays(1);
        return _dbContext.Orders.CountAsync(order => order.CreatedAt >= start && order.CreatedAt < end, cancellationToken);
    }

    public void Add(Order order) => _dbContext.Orders.Add(order);

    public void RemoveItem(OrderItem item) => _dbContext.Set<OrderItem>().Remove(item);
}

internal sealed class EfInventoryStore : IInventoryStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfInventoryStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _dbContext.InventoryItems.FirstOrDefaultAsync(item => item.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyList<InventoryItem>> GetByProductIdsAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().OrderBy(id => id).ToList();
        return await _dbContext.InventoryItems
            .Where(item => ids.Contains(item.ProductId))
            .OrderBy(item => item.ProductId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<InventoryItem>> ListAsync(
        InventoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = _dbContext.InventoryItems.AsNoTracking().AsQueryable();

        if (query.ProductId is not null)
        {
            source = source.Where(item => item.ProductId == query.ProductId);
        }

        if (query.LowStockOnly == true)
        {
            source = source.Where(item => item.QuantityOnHand - item.QuantityReserved <= 5);
        }

        source = source.OrderBy(item => item.ProductId);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<InventoryItem>(items, query.Page, query.PageSize, total);
    }

    public void Add(InventoryItem item) => _dbContext.InventoryItems.Add(item);

    public void AddTransaction(InventoryTransaction transaction) =>
        _dbContext.InventoryTransactions.Add(transaction);
}

internal sealed class EfOrderAuditStore : IOrderAuditStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfOrderAuditStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(OrderAuditEvent auditEvent) => _dbContext.OrderAuditEvents.Add(auditEvent);
}

internal sealed class EfOutboxStore : IOutboxStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfOutboxStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(string eventType, string payloadJson, DateTimeOffset occurredAt, string? correlationId = null)
    {
        _dbContext.OutboxMessages.Add(OutboxMessage.Create(eventType, payloadJson, occurredAt, correlationId));
    }
}

internal sealed class EfOrderNumberGenerator : IOrderNumberGenerator
{
    private readonly IOrderStore _orders;

    public EfOrderNumberGenerator(IOrderStore orders)
    {
        _orders = orders;
    }

    public async Task<OrderNumber> NextAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var count = await _orders.CountCreatedOnUtcDateAsync(utcNow.UtcDateTime.Date, cancellationToken);
        return OrderNumber.Create(utcNow, count + 1);
    }
}
