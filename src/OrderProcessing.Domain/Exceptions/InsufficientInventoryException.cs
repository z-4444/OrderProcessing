namespace OrderProcessing.Domain.Exceptions;

public sealed class InsufficientInventoryException : DomainException
{
    public InsufficientInventoryException(Guid productId, int requested, int available)
        : base($"Insufficient inventory for product {productId}. Requested {requested}, available {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }

    public Guid ProductId { get; }

    public int Requested { get; }

    public int Available { get; }
}
