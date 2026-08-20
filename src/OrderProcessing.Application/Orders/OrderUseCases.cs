using FluentValidation;
using Microsoft.Extensions.Options;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Application.Orders;

public sealed class CreateOrder
{
    private readonly ICustomerStore _customers;
    private readonly IProductStore _products;
    private readonly IOrderStore _orders;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly IApplicationPersistence _persistence;
    private readonly OrderPricingCalculator _pricingCalculator;
    private readonly PricingOptions _pricing;
    private readonly IValidator<CreateOrderRequest> _validator;

    public CreateOrder(
        ICustomerStore customers,
        IProductStore products,
        IOrderStore orders,
        IOrderNumberGenerator orderNumbers,
        IApplicationPersistence persistence,
        OrderPricingCalculator pricingCalculator,
        IOptions<PricingOptions> pricing,
        IValidator<CreateOrderRequest> validator)
    {
        _customers = customers;
        _products = products;
        _orders = orders;
        _orderNumbers = orderNumbers;
        _persistence = persistence;
        _pricingCalculator = pricingCalculator;
        _pricing = pricing.Value;
        _validator = validator;
    }

    public async Task<OrderResponse> Handle(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException($"Customer '{request.CustomerId}' was not found.");

        if (customer.Status != CustomerStatus.Active)
        {
            throw new ConflictException("Inactive customers cannot be used on new orders.");
        }

        var products = await LoadProducts(request.Items, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;
        var order = Order.CreateDraft(
            customer.Id,
            customer.Segment,
            await _orderNumbers.NextAsync(utcNow, cancellationToken),
            _pricing.Currency,
            _pricing.TaxRate,
            utcNow,
            request.Notes);

        AddItems(order, request.Items, products);
        order.RecalculatePricing(_pricingCalculator);

        _orders.Add(order);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }

    private async Task<IReadOnlyDictionary<Guid, Product>> LoadProducts(
        IReadOnlyList<OrderItemRequest> items,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _products.GetByIdsAsync(ids, cancellationToken);
        if (products.Count != ids.Count)
        {
            throw new NotFoundException("One or more products were not found.");
        }

        var inactive = products.FirstOrDefault(product => !product.IsActive);
        if (inactive is not null)
        {
            throw new ConflictException($"Product '{inactive.Sku.Value}' is inactive and cannot be added to an order.");
        }

        return products.ToDictionary(product => product.Id);
    }

    private static void AddItems(
        Order order,
        IReadOnlyList<OrderItemRequest> items,
        IReadOnlyDictionary<Guid, Product> products)
    {
        foreach (var item in items)
        {
            var product = products[item.ProductId];
            order.AddItem(
                product.Id,
                product.Sku,
                product.Name,
                item.Quantity,
                product.UnitPrice,
                new Money(item.LineDiscount, order.Currency));
        }
    }
}

public sealed class UpdateDraftOrder
{
    private readonly IProductStore _products;
    private readonly IOrderStore _orders;
    private readonly IApplicationPersistence _persistence;
    private readonly OrderPricingCalculator _pricingCalculator;
    private readonly IValidator<UpdateDraftOrderRequest> _validator;

    public UpdateDraftOrder(
        IProductStore products,
        IOrderStore orders,
        IApplicationPersistence persistence,
        OrderPricingCalculator pricingCalculator,
        IValidator<UpdateDraftOrderRequest> validator)
    {
        _products = products;
        _orders = orders;
        _persistence = persistence;
        _pricingCalculator = pricingCalculator;
        _validator = validator;
    }

    public async Task<OrderResponse> Handle(Guid id, UpdateDraftOrderRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var order = await _orders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");

        if (!order.IsEditable)
        {
            throw new ConflictException("Only draft orders can be edited.");
        }

        var ids = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _products.GetByIdsAsync(ids, cancellationToken);
        if (products.Count != ids.Count)
        {
            throw new NotFoundException("One or more products were not found.");
        }

        var inactive = products.FirstOrDefault(product => !product.IsActive);
        if (inactive is not null)
        {
            throw new ConflictException($"Product '{inactive.Sku.Value}' is inactive and cannot be added to an order.");
        }

        var byId = products.ToDictionary(product => product.Id);
        order.ClearItems();
        order.UpdateNotes(request.Notes);
        foreach (var item in request.Items)
        {
            var product = byId[item.ProductId];
            order.AddItem(
                product.Id,
                product.Sku,
                product.Name,
                item.Quantity,
                product.UnitPrice,
                new Money(item.LineDiscount, order.Currency));
        }

        order.RecalculatePricing(_pricingCalculator);
        await _persistence.SaveChangesAsync(cancellationToken);
        return order.ToResponse();
    }
}

public sealed class GetOrder
{
    private readonly IOrderStore _orders;

    public GetOrder(IOrderStore orders)
    {
        _orders = orders;
    }

    public async Task<OrderResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Order '{id}' was not found.");

        return order.ToResponse();
    }
}

public sealed class ListOrders
{
    private readonly IOrderStore _orders;

    public ListOrders(IOrderStore orders)
    {
        _orders = orders;
    }

    public async Task<PagedResult<OrderResponse>> Handle(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var result = await _orders.ListAsync(page, pageSize, cancellationToken);
        return new PagedResult<OrderResponse>(
            result.Items.Select(item => item.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
