using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Domain.Orders;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    private OrderItem(
        Guid id,
        Guid productId,
        Sku sku,
        string productName,
        int quantity,
        Money unitPrice,
        Money discount,
        Money lineTotal)
    {
        Id = id;
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Discount = discount;
        LineTotal = lineTotal;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public Sku Sku { get; }

    public string ProductName { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money Discount { get; }

    public Money LineTotal { get; }

    public static OrderItem Create(
        Guid productId,
        Sku sku,
        string productName,
        int quantity,
        Money unitPrice,
        Money lineDiscount)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product id is required.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("Product name is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        EnsureSameCurrency(unitPrice, lineDiscount);

        var gross = unitPrice.Multiply(quantity);
        if (lineDiscount.Amount > gross.Amount)
        {
            throw new DomainException("Line discount cannot exceed the line gross amount.");
        }

        var lineTotal = gross.Subtract(lineDiscount);

        return new OrderItem(
            Guid.NewGuid(),
            productId,
            sku,
            productName.Trim(),
            quantity,
            unitPrice,
            lineDiscount,
            lineTotal);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException($"Currency mismatch: {left.Currency} vs {right.Currency}.");
        }
    }
}
