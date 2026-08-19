namespace OrderProcessing.Domain.Inventory;

public enum InventoryTransactionType
{
    Adjustment = 0,
    Reserve = 1,
    Release = 2,
    Sale = 3
}
