using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Pricing;

namespace OrderProcessing.UnitTests.Pricing;

public class LoyalCustomerDiscountPolicyTests
{
    private readonly LoyalCustomerDiscountPolicy _policy = new();

    [Fact]
    public void LoyalCustomer_WithSubtotalAtThreshold_AppliesTenPercentDiscount()
    {
        var subtotal = new Money(100m, "USD");

        var discount = _policy.CalculateOrderDiscount(subtotal, CustomerSegment.Loyal);

        Assert.Equal(10m, discount.Amount);
    }

    [Fact]
    public void LoyalCustomer_BelowThreshold_AppliesNoDiscount()
    {
        var subtotal = new Money(99.99m, "USD");

        var discount = _policy.CalculateOrderDiscount(subtotal, CustomerSegment.Loyal);

        Assert.Equal(0m, discount.Amount);
    }

    [Fact]
    public void NewCustomer_AboveThreshold_AppliesNoDiscount()
    {
        var subtotal = new Money(250m, "USD");

        var discount = _policy.CalculateOrderDiscount(subtotal, CustomerSegment.New);

        Assert.Equal(0m, discount.Amount);
    }
}

public class OrderPricingCalculatorTests
{
    private readonly OrderPricingCalculator _calculator = new(new LoyalCustomerDiscountPolicy());

    [Fact]
    public void Calculate_ForLoyalOrderAboveThreshold_AppliesDiscountTaxAndGrandTotal()
    {
        var order = OrderProcessing.UnitTests.Helpers.OrderTestFactory.CreateDraftOrderWithItem(
            120m,
            segment: CustomerSegment.Loyal);

        var result = _calculator.Calculate(order.Items, CustomerSegment.Loyal, taxRate: 0.10m);

        Assert.Equal(120m, result.Subtotal.Amount);
        Assert.Equal(12m, result.Discount.Amount);
        Assert.Equal(10.80m, result.Tax.Amount);
        Assert.Equal(118.80m, result.GrandTotal.Amount);
    }

    [Fact]
    public void Calculate_ForNewCustomer_DoesNotApplyLoyalDiscount()
    {
        var order = OrderProcessing.UnitTests.Helpers.OrderTestFactory.CreateDraftOrderWithItem(
            120m,
            segment: CustomerSegment.New);

        var result = _calculator.Calculate(order.Items, CustomerSegment.New, taxRate: 0.10m);

        Assert.Equal(0m, result.Discount.Amount);
        Assert.Equal(132m, result.GrandTotal.Amount);
    }
}
