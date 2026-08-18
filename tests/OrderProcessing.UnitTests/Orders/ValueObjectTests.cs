using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.UnitTests.Orders;

public class OrderItemTests
{
    [Fact]
    public void Create_CalculatesLineTotalFromUnitPriceQuantityAndDiscount()
    {
        var item = OrderItem.Create(
            Guid.NewGuid(),
            new Sku("sku-001"),
            "Widget",
            quantity: 2,
            unitPrice: new Money(25m, "USD"),
            lineDiscount: new Money(5m, "USD"));

        Assert.Equal("SKU-001", item.Sku.Value);
        Assert.Equal(45m, item.LineTotal.Amount);
    }

    [Fact]
    public void Create_WhenDiscountExceedsGross_Throws()
    {
        Assert.Throws<DomainException>(() =>
            OrderItem.Create(
                Guid.NewGuid(),
                new Sku("SKU-002"),
                "Widget",
                quantity: 1,
                unitPrice: new Money(10m, "USD"),
                lineDiscount: new Money(11m, "USD")));
    }
}

public class MoneyTests
{
    [Fact]
    public void Add_WithMatchingCurrency_ReturnsSum()
    {
        var left = new Money(10.005m, "usd");
        var right = new Money(2.004m, "USD");

        var result = left.Add(right);

        Assert.Equal(12.01m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Subtract_WhenResultWouldBeNegative_Throws()
    {
        var left = new Money(5m, "USD");
        var right = new Money(6m, "USD");

        Assert.Throws<DomainException>(() => left.Subtract(right));
    }
}

public class OrderNumberTests
{
    [Fact]
    public void Create_GeneratesFormattedOrderNumber()
    {
        var timestamp = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        var orderNumber = OrderNumber.Create(timestamp, 42);

        Assert.Equal("ORD-20260818-0042", orderNumber.Value);
    }
}

public class SkuTests
{
    [Fact]
    public void Constructor_NormalizesValue()
    {
        var sku = new Sku(" abc-123 ");

        Assert.Equal("ABC-123", sku.Value);
    }
}
