using FluentValidation;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Inventory;

namespace OrderProcessing.Application.Inventory;

public sealed record InventoryItemResponse(
    Guid ProductId,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity);

public sealed record AdjustInventoryRequest(int Delta, string? Reason);

public static class InventoryMappings
{
    public static InventoryItemResponse ToResponse(this InventoryItem item) =>
        new(item.ProductId, item.QuantityOnHand, item.QuantityReserved, item.AvailableQuantity);
}

public sealed class AdjustInventoryRequestValidator : AbstractValidator<AdjustInventoryRequest>
{
    public AdjustInventoryRequestValidator()
    {
        RuleFor(request => request.Delta).NotEqual(0);
        RuleFor(request => request.Reason).MaximumLength(500);
    }
}

public sealed class GetInventory
{
    private readonly IInventoryStore _inventory;

    public GetInventory(IInventoryStore inventory)
    {
        _inventory = inventory;
    }

    public async Task<InventoryItemResponse> Handle(Guid productId, CancellationToken cancellationToken = default)
    {
        var item = await _inventory.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Inventory for product '{productId}' was not found.");

        return item.ToResponse();
    }
}

public sealed class ListInventory
{
    private readonly IInventoryStore _inventory;

    public ListInventory(IInventoryStore inventory)
    {
        _inventory = inventory;
    }

    public async Task<PagedResult<InventoryItemResponse>> Handle(
        InventoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await _inventory.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        return new PagedResult<InventoryItemResponse>(
            result.Items.Select(item => item.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}

public sealed class AdjustInventory
{
    private readonly IInventoryStore _inventory;
    private readonly IApplicationPersistence _persistence;
    private readonly IValidator<AdjustInventoryRequest> _validator;

    public AdjustInventory(
        IInventoryStore inventory,
        IApplicationPersistence persistence,
        IValidator<AdjustInventoryRequest> validator)
    {
        _inventory = inventory;
        _persistence = persistence;
        _validator = validator;
    }

    public async Task<InventoryItemResponse> Handle(
        Guid productId,
        AdjustInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var item = await _inventory.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException($"Inventory for product '{productId}' was not found.");

        item.Adjust(request.Delta);
        _inventory.AddTransaction(InventoryTransaction.Create(
            productId,
            InventoryTransactionType.Adjustment,
            request.Delta,
            DateTimeOffset.UtcNow,
            reason: request.Reason));

        await _persistence.SaveChangesAsync(cancellationToken);
        return item.ToResponse();
    }
}
