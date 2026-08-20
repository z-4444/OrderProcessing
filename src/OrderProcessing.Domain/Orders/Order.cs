using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    private Order(
        Guid id,
        OrderNumber orderNumber,
        Guid customerId,
        CustomerSegment customerSegment,
        OrderStatus status,
        string currency,
        Money subtotal,
        Money discountAmount,
        Money taxAmount,
        Money grandTotal,
        decimal taxRate,
        string? notes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OrderNumber = orderNumber;
        CustomerId = customerId;
        CustomerSegment = customerSegment;
        Status = status;
        Currency = currency;
        Subtotal = subtotal;
        DiscountAmount = discountAmount;
        TaxAmount = taxAmount;
        GrandTotal = grandTotal;
        TaxRate = taxRate;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public OrderNumber OrderNumber { get; }

    public Guid CustomerId { get; }

    public CustomerSegment CustomerSegment { get; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public string Currency { get; private set; }

    public Money Subtotal { get; private set; }

    public Money DiscountAmount { get; private set; }

    public Money TaxAmount { get; private set; }

    public Money GrandTotal { get; private set; }

    public decimal TaxRate { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order CreateDraft(
        Guid customerId,
        CustomerSegment customerSegment,
        OrderNumber orderNumber,
        string currency,
        decimal taxRate,
        DateTimeOffset createdAt,
        string? notes = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("Currency is required.");
        }

        if (taxRate < 0)
        {
            throw new DomainException("Tax rate cannot be negative.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var zero = Money.Zero(normalizedCurrency);

        return new Order(
            Guid.NewGuid(),
            orderNumber,
            customerId,
            customerSegment,
            OrderStatus.Draft,
            normalizedCurrency,
            zero,
            zero,
            zero,
            zero,
            taxRate,
            notes?.Trim(),
            createdAt,
            createdAt);
    }

    public void AddItem(
        Guid productId,
        Sku sku,
        string productName,
        int quantity,
        Money unitPrice,
        Money lineDiscount)
    {
        EnsureDraft("add items to");

        if (!unitPrice.Currency.Equals(Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Unit price currency must match order currency {Currency}.");
        }

        _items.Add(OrderItem.Create(productId, sku, productName, quantity, unitPrice, lineDiscount));
        TouchUpdatedAt();
    }

    public void ClearItems()
    {
        EnsureDraft("clear items on");
        _items.Clear();
        TouchUpdatedAt();
    }

    public void UpdateNotes(string? notes)
    {
        EnsureDraft("update notes on");
        Notes = notes?.Trim();
        TouchUpdatedAt();
    }

    public void RecalculatePricing(OrderPricingCalculator pricingCalculator)
    {
        if (_items.Count == 0)
        {
            var zero = Money.Zero(Currency);
            Subtotal = zero;
            DiscountAmount = zero;
            TaxAmount = zero;
            GrandTotal = zero;
            TouchUpdatedAt();
            return;
        }

        var result = pricingCalculator.Calculate(_items, CustomerSegment, TaxRate);
        Subtotal = result.Subtotal;
        DiscountAmount = result.Discount;
        TaxAmount = result.Tax;
        GrandTotal = result.GrandTotal;
        TaxRate = result.TaxRate;
        TouchUpdatedAt();
    }

    public void Submit(OrderPricingCalculator pricingCalculator)
    {
        EnsureStatus(OrderStatus.Draft, nameof(Submit));
        EnsureHasItems(nameof(Submit));

        RecalculatePricing(pricingCalculator);
        TransitionTo(OrderStatus.Pending);
    }

    public void Confirm(OrderPricingCalculator pricingCalculator)
    {
        EnsureStatus(OrderStatus.Pending, nameof(Confirm));
        EnsureHasItems(nameof(Confirm));

        RecalculatePricing(pricingCalculator);
        TransitionTo(OrderStatus.Confirmed);
    }

    public void StartProcessing()
    {
        EnsureStatus(OrderStatus.Confirmed, nameof(StartProcessing));
        TransitionTo(OrderStatus.Processing);
    }

    public void Ship()
    {
        EnsureStatus(OrderStatus.Processing, nameof(Ship));
        TransitionTo(OrderStatus.Shipped);
    }

    public void Complete()
    {
        EnsureStatus(OrderStatus.Shipped, nameof(Complete));
        TransitionTo(OrderStatus.Completed);
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Pending or OrderStatus.Confirmed))
        {
            throw new InvalidOrderTransitionException(Status, "cancel");
        }

        TransitionTo(OrderStatus.Cancelled);
    }

    public void Fail()
    {
        if (Status is not (OrderStatus.Confirmed or OrderStatus.Processing))
        {
            throw new InvalidOrderTransitionException(Status, "mark as failed");
        }

        TransitionTo(OrderStatus.Failed);
    }

    public bool CanCancel => Status is OrderStatus.Draft or OrderStatus.Pending or OrderStatus.Confirmed;

    public bool IsEditable => Status == OrderStatus.Draft;

    private void EnsureDraft(string action)
    {
        if (Status != OrderStatus.Draft)
        {
            throw new InvalidOrderTransitionException(Status, action);
        }
    }

    private void EnsureStatus(OrderStatus requiredStatus, string action)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOrderTransitionException(Status, action);
        }
    }

    private void EnsureHasItems(string action)
    {
        if (_items.Count == 0)
        {
            throw new DomainException($"Cannot {action} an order with no items.");
        }
    }

    private void TransitionTo(OrderStatus nextStatus)
    {
        Status = nextStatus;
        TouchUpdatedAt();
    }

    private void TouchUpdatedAt()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
