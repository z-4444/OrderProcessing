using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.UnitTests.Helpers;

internal static class OrderTestFactory
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);

    public static Order CreateDraftOrder(CustomerSegment segment = CustomerSegment.New)
    {
        return Order.CreateDraft(
            Guid.NewGuid(),
            segment,
            new OrderNumber("ORD-TEST-0001"),
            "USD",
            taxRate: 0.08m,
            CreatedAt);
    }

    public static Order CreateDraftOrderWithItem(
        decimal unitPrice,
        int quantity = 1,
        CustomerSegment segment = CustomerSegment.New,
        Money? lineDiscount = null)
    {
        var order = CreateDraftOrder(segment);
        order.AddItem(
            Guid.NewGuid(),
            new Sku("SKU-001"),
            "Sample Product",
            quantity,
            new Money(unitPrice, "USD"),
            lineDiscount ?? Money.Zero("USD"));
        return order;
    }

    public static OrderPricingCalculator CreatePricingCalculator()
    {
        return new OrderPricingCalculator(new LoyalCustomerDiscountPolicy());
    }
}
