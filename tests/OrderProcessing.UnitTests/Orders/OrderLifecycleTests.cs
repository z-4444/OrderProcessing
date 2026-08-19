using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;
using OrderProcessing.UnitTests.Helpers;

namespace OrderProcessing.UnitTests.Orders;

public class OrderLifecycleTests
{
    private readonly OrderPricingCalculator _pricingCalculator = OrderTestFactory.CreatePricingCalculator();

    [Fact]
    public void Submit_FromDraftWithItems_MovesToPending()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);

        order.Submit(_pricingCalculator);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.True(order.Subtotal.Amount > 0);
    }

    [Fact]
    public void Confirm_FromPending_MovesToConfirmed()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);
        order.Submit(_pricingCalculator);

        order.Confirm(_pricingCalculator);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void FullHappyPath_ReachesCompleted()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(120m, segment: CustomerSegment.Loyal);
        order.Submit(_pricingCalculator);
        order.Confirm(_pricingCalculator);
        order.StartProcessing();
        order.Ship();
        order.Complete();

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Failed)]
    public void Submit_FromNonDraft_Throws(OrderStatus status)
    {
        var order = AdvanceToStatus(status);

        var exception = Assert.Throws<InvalidOrderTransitionException>(() => order.Submit(_pricingCalculator));
        Assert.Equal(status, exception.CurrentStatus);
    }

    [Fact]
    public void Confirm_FromDraft_Throws()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);

        Assert.Throws<InvalidOrderTransitionException>(() => order.Confirm(_pricingCalculator));
    }

    [Fact]
    public void StartProcessing_FromConfirmed_MovesToProcessing()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);
        order.Submit(_pricingCalculator);
        order.Confirm(_pricingCalculator);

        order.StartProcessing();

        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public void Ship_FromProcessing_MovesToShipped()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);
        order.Submit(_pricingCalculator);
        order.Confirm(_pricingCalculator);
        order.StartProcessing();

        order.Ship();

        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public void Cancel_FromAllowedStatuses_MovesToCancelled(OrderStatus fromStatus)
    {
        var order = AdvanceToStatus(fromStatus);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Failed)]
    public void Cancel_FromDisallowedStatuses_Throws(OrderStatus status)
    {
        var order = AdvanceToStatus(status);

        Assert.Throws<InvalidOrderTransitionException>(() => order.Cancel());
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Processing)]
    public void Fail_FromAllowedStatuses_MovesToFailed(OrderStatus fromStatus)
    {
        var order = AdvanceToStatus(fromStatus);

        order.Fail();

        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void Fail_FromDisallowedStatuses_Throws(OrderStatus status)
    {
        var order = AdvanceToStatus(status);

        Assert.Throws<InvalidOrderTransitionException>(() => order.Fail());
    }

    [Fact]
    public void AddItem_WhenNotDraft_Throws()
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(50m);
        order.Submit(_pricingCalculator);

        Assert.Throws<InvalidOrderTransitionException>(() =>
            order.AddItem(
                Guid.NewGuid(),
                new Sku("SKU-002"),
                "Another Product",
                1,
                new Money(10m, "USD"),
                Money.Zero("USD")));
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var order = OrderTestFactory.CreateDraftOrder();

        Assert.Throws<DomainException>(() => order.Submit(_pricingCalculator));
    }

    private Order AdvanceToStatus(OrderStatus targetStatus)
    {
        var order = OrderTestFactory.CreateDraftOrderWithItem(100m, segment: CustomerSegment.Loyal);

        switch (targetStatus)
        {
            case OrderStatus.Draft:
                return order;
            case OrderStatus.Pending:
                order.Submit(_pricingCalculator);
                return order;
            case OrderStatus.Confirmed:
                order.Submit(_pricingCalculator);
                order.Confirm(_pricingCalculator);
                return order;
            case OrderStatus.Processing:
                order.Submit(_pricingCalculator);
                order.Confirm(_pricingCalculator);
                order.StartProcessing();
                return order;
            case OrderStatus.Shipped:
                order.Submit(_pricingCalculator);
                order.Confirm(_pricingCalculator);
                order.StartProcessing();
                order.Ship();
                return order;
            case OrderStatus.Completed:
                order.Submit(_pricingCalculator);
                order.Confirm(_pricingCalculator);
                order.StartProcessing();
                order.Ship();
                order.Complete();
                return order;
            case OrderStatus.Cancelled:
                var cancellable = OrderTestFactory.CreateDraftOrderWithItem(100m);
                cancellable.Cancel();
                return cancellable;
            case OrderStatus.Failed:
                order.Submit(_pricingCalculator);
                order.Confirm(_pricingCalculator);
                order.Fail();
                return order;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetStatus), targetStatus, null);
        }
    }
}
