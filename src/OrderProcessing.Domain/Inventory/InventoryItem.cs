using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Inventory;

public sealed class InventoryItem
{
    private InventoryItem()
    {
    }

    public InventoryItem(Guid productId, int quantityOnHand)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product id is required.");
        }

        if (quantityOnHand < 0)
        {
            throw new DomainException("Quantity on hand cannot be negative.");
        }

        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        QuantityReserved = 0;
    }

    public Guid ProductId { get; }

    public int QuantityOnHand { get; private set; }

    public int QuantityReserved { get; private set; }

    public int AvailableQuantity => QuantityOnHand - QuantityReserved;

    public void Adjust(int delta)
    {
        var newOnHand = QuantityOnHand + delta;
        if (newOnHand < QuantityReserved)
        {
            throw new DomainException("Adjustment would make on-hand quantity less than reserved quantity.");
        }

        if (newOnHand < 0)
        {
            throw new DomainException("Quantity on hand cannot be negative.");
        }

        QuantityOnHand = newOnHand;
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Reserve quantity must be greater than zero.");
        }

        if (quantity > AvailableQuantity)
        {
            throw new InsufficientInventoryException(ProductId, quantity, AvailableQuantity);
        }

        QuantityReserved += quantity;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Release quantity must be greater than zero.");
        }

        if (quantity > QuantityReserved)
        {
            throw new DomainException("Cannot release more quantity than is currently reserved.");
        }

        QuantityReserved -= quantity;
    }

    public void CommitSale(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Sale quantity must be greater than zero.");
        }

        if (quantity > QuantityReserved)
        {
            throw new DomainException("Cannot commit a sale for more quantity than is currently reserved.");
        }

        if (quantity > QuantityOnHand)
        {
            throw new DomainException("Cannot commit a sale for more quantity than is on hand.");
        }

        QuantityOnHand -= quantity;
        QuantityReserved -= quantity;
    }
}
