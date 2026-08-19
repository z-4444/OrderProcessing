using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Domain.Exceptions;

public sealed class InvalidOrderTransitionException : DomainException
{
    public InvalidOrderTransitionException(OrderStatus currentStatus, string action)
        : base($"Cannot {action} an order in {currentStatus} status.")
    {
        CurrentStatus = currentStatus;
        Action = action;
    }

    public OrderStatus CurrentStatus { get; }

    public string Action { get; }
}
