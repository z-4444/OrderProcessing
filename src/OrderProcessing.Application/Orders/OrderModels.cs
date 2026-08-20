using FluentValidation;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders;

public sealed record OrderItemRequest(Guid ProductId, int Quantity, decimal LineDiscount = 0m);

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderItemRequest> Items, string? Notes);

public sealed record UpdateDraftOrderRequest(IReadOnlyList<OrderItemRequest> Items, string? Notes);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal LineTotal,
    string Currency);

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerSegment,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal GrandTotal,
    decimal TaxRate,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderItemResponse> Items);

public static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order) =>
        new(
            order.Id,
            order.OrderNumber.Value,
            order.CustomerId,
            order.CustomerSegment.ToString(),
            order.Status.ToString(),
            order.Currency,
            order.Subtotal.Amount,
            order.DiscountAmount.Amount,
            order.TaxAmount.Amount,
            order.GrandTotal.Amount,
            order.TaxRate,
            order.Notes,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(item => new OrderItemResponse(
                item.Id,
                item.ProductId,
                item.Sku.Value,
                item.ProductName,
                item.Quantity,
                item.UnitPrice.Amount,
                item.Discount.Amount,
                item.LineTotal.Amount,
                item.UnitPrice.Currency)).ToList());
}

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.CustomerId).NotEmpty();
        RuleFor(request => request.Items).NotEmpty();
        RuleForEach(request => request.Items).SetValidator(new OrderItemRequestValidator());
        RuleFor(request => request.Notes).MaximumLength(2000);
    }
}

public sealed class UpdateDraftOrderRequestValidator : AbstractValidator<UpdateDraftOrderRequest>
{
    public UpdateDraftOrderRequestValidator()
    {
        RuleFor(request => request.Items).NotEmpty();
        RuleForEach(request => request.Items).SetValidator(new OrderItemRequestValidator());
        RuleFor(request => request.Notes).MaximumLength(2000);
    }
}

public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(request => request.ProductId).NotEmpty();
        RuleFor(request => request.Quantity).GreaterThan(0);
        RuleFor(request => request.LineDiscount).GreaterThanOrEqualTo(0);
    }
}
