using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.Inventory;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController : ControllerBase
{
    private readonly GetInventory _getInventory;
    private readonly ListInventory _listInventory;
    private readonly AdjustInventory _adjustInventory;

    public InventoryController(
        GetInventory getInventory,
        ListInventory listInventory,
        AdjustInventory adjustInventory)
    {
        _getInventory = getInventory;
        _listInventory = listInventory;
        _adjustInventory = adjustInventory;
    }

    [HttpGet]
    [Authorize(Policy = Policies.InventoryRead)]
    [ProducesResponseType(typeof(PagedResult<InventoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InventoryItemResponse>>> List(
        [FromQuery] InventoryListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _listInventory.Handle(query, cancellationToken));

    [HttpGet("{productId:guid}")]
    [Authorize(Policy = Policies.InventoryRead)]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryItemResponse>> Get(Guid productId, CancellationToken cancellationToken) =>
        Ok(await _getInventory.Handle(productId, cancellationToken));

    [HttpPost("{productId:guid}/adjust")]
    [Authorize(Policy = Policies.InventoryAdjust)]
    [ProducesResponseType(typeof(InventoryItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventoryItemResponse>> Adjust(
        Guid productId,
        [FromBody] AdjustInventoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _adjustInventory.Handle(productId, request, cancellationToken));
}
