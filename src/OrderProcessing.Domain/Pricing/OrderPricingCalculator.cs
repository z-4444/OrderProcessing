using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;

namespace OrderProcessing.Domain.Pricing;

public interface ICustomerDiscountPolicy
{
    Money CalculateOrderDiscount(Money subtotal, CustomerSegment segment);
}

public sealed class LoyalCustomerDiscountPolicy : ICustomerDiscountPolicy
{
    public const decimal LoyalDiscountRate = 0.10m;
    public const decimal LoyalDiscountThreshold = 100m;

    public Money CalculateOrderDiscount(Money subtotal, CustomerSegment segment)
    {
        if (segment != CustomerSegment.Loyal || subtotal.Amount < LoyalDiscountThreshold)
        {
            return Money.Zero(subtotal.Currency);
        }

        return subtotal.Multiply(LoyalDiscountRate);
    }
}

public sealed class OrderPricingCalculator
{
    public OrderPricingCalculator(ICustomerDiscountPolicy discountPolicy)
    {
        DiscountPolicy = discountPolicy;
    }

    public ICustomerDiscountPolicy DiscountPolicy { get; }

    public OrderPricingResult Calculate(
        IReadOnlyCollection<OrderProcessing.Domain.Orders.OrderItem> items,
        CustomerSegment segment,
        decimal taxRate)
    {
        if (items.Count == 0)
        {
            throw new OrderProcessing.Domain.Exceptions.DomainException("At least one order item is required to calculate pricing.");
        }

        if (taxRate < 0)
        {
            throw new OrderProcessing.Domain.Exceptions.DomainException("Tax rate cannot be negative.");
        }

        var currency = items.First().UnitPrice.Currency;
        var subtotal = items
            .Select(item => item.LineTotal)
            .Aggregate(Money.Zero(currency), (current, lineTotal) => current.Add(lineTotal));

        var discount = DiscountPolicy.CalculateOrderDiscount(subtotal, segment);
        var taxableBase = subtotal.Subtract(discount);
        var tax = taxableBase.Multiply(taxRate);
        var grandTotal = taxableBase.Add(tax);

        return new OrderPricingResult(subtotal, discount, tax, grandTotal, taxRate);
    }
}

public readonly record struct OrderPricingResult(
    Money Subtotal,
    Money Discount,
    Money Tax,
    Money GrandTotal,
    decimal TaxRate);
