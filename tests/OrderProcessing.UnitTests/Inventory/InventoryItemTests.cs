using OrderProcessing.Domain.Exceptions;
using OrderProcessing.Domain.Inventory;

namespace OrderProcessing.UnitTests.Inventory;

public class InventoryItemTests
{
    [Fact]
    public void Reserve_WithSufficientAvailableQuantity_IncreasesReserved()
    {
        var inventory = new InventoryItem(Guid.NewGuid(), quantityOnHand: 10);

        inventory.Reserve(4);

        Assert.Equal(4, inventory.QuantityReserved);
        Assert.Equal(6, inventory.AvailableQuantity);
    }

    [Fact]
    public void Reserve_WhenInsufficientAvailableQuantity_Throws()
    {
        var productId = Guid.NewGuid();
        var inventory = new InventoryItem(productId, quantityOnHand: 3);

        var exception = Assert.Throws<InsufficientInventoryException>(() => inventory.Reserve(4));

        Assert.Equal(productId, exception.ProductId);
        Assert.Equal(4, exception.Requested);
        Assert.Equal(3, exception.Available);
    }

    [Fact]
    public void Release_ReducesReservedQuantity()
    {
        var inventory = new InventoryItem(Guid.NewGuid(), quantityOnHand: 10);
        inventory.Reserve(5);

        inventory.Release(2);

        Assert.Equal(3, inventory.QuantityReserved);
        Assert.Equal(7, inventory.AvailableQuantity);
    }

    [Fact]
    public void CommitSale_ReducesOnHandAndReserved()
    {
        var inventory = new InventoryItem(Guid.NewGuid(), quantityOnHand: 10);
        inventory.Reserve(4);

        inventory.CommitSale(4);

        Assert.Equal(6, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.QuantityReserved);
    }

    [Fact]
    public void Adjust_WhenResultWouldBeBelowReserved_Throws()
    {
        var inventory = new InventoryItem(Guid.NewGuid(), quantityOnHand: 10);
        inventory.Reserve(8);

        Assert.Throws<DomainException>(() => inventory.Adjust(-5));
    }
}
