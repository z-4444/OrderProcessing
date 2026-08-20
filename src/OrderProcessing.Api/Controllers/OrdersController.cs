using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.Orders;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly CreateOrder _createOrder;
    private readonly UpdateDraftOrder _updateDraftOrder;
    private readonly GetOrder _getOrder;
    private readonly ListOrders _listOrders;

    public OrdersController(
        CreateOrder createOrder,
        UpdateDraftOrder updateDraftOrder,
        GetOrder getOrder,
        ListOrders listOrders)
    {
        _createOrder = createOrder;
        _updateDraftOrder = updateDraftOrder;
        _getOrder = getOrder;
        _listOrders = listOrders;
    }

    [HttpGet]
    [Authorize(Policy = Policies.OrdersRead)]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _listOrders.Handle(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.OrdersRead)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _getOrder.Handle(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.OrdersCreate)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _createOrder.Handle(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OrdersEdit)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Update(
        Guid id,
        [FromBody] UpdateDraftOrderRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _updateDraftOrder.Handle(id, request, cancellationToken));
}
