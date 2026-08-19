using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Inventory;

public sealed class InventoryTransaction
{
    private InventoryTransaction()
    {
    }

    private InventoryTransaction(
        Guid id,
        Guid productId,
        Guid? orderId,
        InventoryTransactionType type,
        int quantity,
        string? reason,
        DateTimeOffset createdAt)
    {
        Id = id;
        ProductId = productId;
        OrderId = orderId;
        Type = type;
        Quantity = quantity;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public Guid? OrderId { get; }

    public InventoryTransactionType Type { get; }

    public int Quantity { get; }

    public string? Reason { get; }

    public DateTimeOffset CreatedAt { get; }

    public static InventoryTransaction Create(
        Guid productId,
        InventoryTransactionType type,
        int quantity,
        DateTimeOffset createdAt,
        Guid? orderId = null,
        string? reason = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product id is required.");
        }

        if (quantity == 0)
        {
            throw new DomainException("Transaction quantity cannot be zero.");
        }

        return new InventoryTransaction(
            Guid.NewGuid(),
            productId,
            orderId,
            type,
            quantity,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            createdAt);
    }
}
