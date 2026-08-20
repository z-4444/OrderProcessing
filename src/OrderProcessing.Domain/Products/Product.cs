using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Products;

public sealed class Product
{
    private Product()
    {
    }

    private Product(
        Guid id,
        Sku sku,
        string name,
        string? description,
        Money unitPrice,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Sku = sku;
        Name = name;
        Description = description;
        UnitPrice = unitPrice;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public Sku Sku { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Money UnitPrice { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Product Create(
        Guid id,
        Sku sku,
        string name,
        Money unitPrice,
        DateTimeOffset createdAt,
        string? description = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Product id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        if (unitPrice.Amount <= 0)
        {
            throw new DomainException("Product unit price must be greater than zero.");
        }

        return new Product(
            id,
            sku,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            unitPrice,
            isActive: true,
            createdAt,
            createdAt);
    }

    public void Update(
        string name,
        Money unitPrice,
        DateTimeOffset updatedAt,
        string? description = null,
        bool? isActive = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name is required.");
        }

        if (unitPrice.Amount <= 0)
        {
            throw new DomainException("Product unit price must be greater than zero.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnitPrice = unitPrice;
        if (isActive.HasValue)
        {
            IsActive = isActive.Value;
        }

        UpdatedAt = updatedAt;
    }
}
