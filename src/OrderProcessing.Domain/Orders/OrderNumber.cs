using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Orders;

public readonly record struct OrderNumber
{
    public OrderNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Order number is required.");
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 32)
        {
            throw new DomainException("Order number cannot exceed 32 characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public static OrderNumber Create(DateTimeOffset timestamp, int sequence)
    {
        if (sequence < 0 || sequence > 9999)
        {
            throw new DomainException("Order sequence must be between 0 and 9999.");
        }

        return new OrderNumber($"ORD-{timestamp:yyyyMMdd}-{sequence:D4}");
    }

    public override string ToString() => Value;
}
