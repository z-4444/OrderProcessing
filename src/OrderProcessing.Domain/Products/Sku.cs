using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Products;

public readonly record struct Sku
{
    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("SKU is required.");
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 64)
        {
            throw new DomainException("SKU cannot exceed 64 characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
