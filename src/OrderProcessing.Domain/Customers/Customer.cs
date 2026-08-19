using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Customers;

public sealed class Customer
{
    private Customer()
    {
    }

    private Customer(
        Guid id,
        string name,
        string email,
        string? phone,
        CustomerStatus status,
        CustomerSegment segment,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Email = email;
        Phone = phone;
        Status = status;
        Segment = segment;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public CustomerStatus Status { get; private set; }

    public CustomerSegment Segment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Customer Create(
        Guid id,
        string name,
        string email,
        CustomerSegment segment,
        DateTimeOffset createdAt,
        string? phone = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Customer email is required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@'))
        {
            throw new DomainException("Customer email is invalid.");
        }

        return new Customer(
            id,
            name.Trim(),
            normalizedEmail,
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            CustomerStatus.Active,
            segment,
            createdAt,
            createdAt);
    }
}
