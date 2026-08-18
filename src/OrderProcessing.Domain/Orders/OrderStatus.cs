namespace OrderProcessing.Domain.Orders;

public enum OrderStatus
{
    Draft = 0,
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6,
    Failed = 7
}
