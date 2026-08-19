namespace OrderProcessing.Domain.Orders;

public enum OrderAuditEventType
{
    Created = 0,
    Submitted = 1,
    Confirmed = 2,
    ProcessingStarted = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6,
    Failed = 7,
    InventoryReserved = 8,
    InventoryReleased = 9
}
