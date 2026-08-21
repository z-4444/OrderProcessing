using FluentValidation;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Application.Products;

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    decimal UnitPrice,
    string? Description,
    string Currency = "USD",
    int InitialQuantity = 0);

public sealed record UpdateProductRequest(string Name, decimal UnitPrice, bool IsActive, string? Description, string Currency = "USD");

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class ProductMappings
{
    public static ProductResponse ToResponse(this Product product) =>
        new(
            product.Id,
            product.Sku.Value,
            product.Name,
            product.Description,
            product.UnitPrice.Amount,
            product.UnitPrice.Currency,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt);
}

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Sku).NotEmpty().MaximumLength(64);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.UnitPrice).GreaterThan(0);
        RuleFor(request => request.Currency).NotEmpty().Length(3);
        RuleFor(request => request.InitialQuantity).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.UnitPrice).GreaterThan(0);
        RuleFor(request => request.Currency).NotEmpty().Length(3);
    }
}
