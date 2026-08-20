using FluentValidation;
using OrderProcessing.Domain.Customers;

namespace OrderProcessing.Application.Customers;

public sealed record CreateCustomerRequest(string Name, string Email, string Segment, string? Phone);

public sealed record UpdateCustomerRequest(string Name, string Email, string Segment, string Status, string? Phone);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string Status,
    string Segment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class CustomerMappings
{
    public static CustomerResponse ToResponse(this Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Status.ToString(),
            customer.Segment.ToString(),
            customer.CreatedAt,
            customer.UpdatedAt);
}

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Phone).MaximumLength(32);
        RuleFor(request => request.Segment).NotEmpty().Must(BeSegment);
    }

    private static bool BeSegment(string value) => Enum.TryParse<CustomerSegment>(value, ignoreCase: true, out _);
}

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Phone).MaximumLength(32);
        RuleFor(request => request.Segment).NotEmpty().Must(value => Enum.TryParse<CustomerSegment>(value, true, out _));
        RuleFor(request => request.Status).NotEmpty().Must(value => Enum.TryParse<CustomerStatus>(value, true, out _));
    }
}
