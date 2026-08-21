using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Abstractions;
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
    private readonly SubmitOrder _submitOrder;
    private readonly ConfirmOrder _confirmOrder;
    private readonly StartProcessingOrder _startProcessingOrder;
    private readonly ShipOrder _shipOrder;
    private readonly CompleteOrder _completeOrder;
    private readonly CancelOrder _cancelOrder;
    private readonly FailOrder _failOrder;

    public OrdersController(
        CreateOrder createOrder,
        UpdateDraftOrder updateDraftOrder,
        GetOrder getOrder,
        ListOrders listOrders,
        SubmitOrder submitOrder,
        ConfirmOrder confirmOrder,
        StartProcessingOrder startProcessingOrder,
        ShipOrder shipOrder,
        CompleteOrder completeOrder,
        CancelOrder cancelOrder,
        FailOrder failOrder)
    {
        _createOrder = createOrder;
        _updateDraftOrder = updateDraftOrder;
        _getOrder = getOrder;
        _listOrders = listOrders;
        _submitOrder = submitOrder;
        _confirmOrder = confirmOrder;
        _startProcessingOrder = startProcessingOrder;
        _shipOrder = shipOrder;
        _completeOrder = completeOrder;
        _cancelOrder = cancelOrder;
        _failOrder = failOrder;
    }

    [HttpGet]
    [Authorize(Policy = Policies.OrdersRead)]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] OrderListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _listOrders.Handle(query, cancellationToken));

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

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.OrdersSubmit)]
    public async Task<ActionResult<OrderResponse>> Submit(Guid id, CancellationToken cancellationToken) =>
        Ok(await _submitOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Policies.OrdersConfirm)]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid id, CancellationToken cancellationToken) =>
        Ok(await _confirmOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/process")]
    [Authorize(Policy = Policies.OrdersProcess)]
    public async Task<ActionResult<OrderResponse>> Process(Guid id, CancellationToken cancellationToken) =>
        Ok(await _startProcessingOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/ship")]
    [Authorize(Policy = Policies.OrdersShip)]
    public async Task<ActionResult<OrderResponse>> Ship(Guid id, CancellationToken cancellationToken) =>
        Ok(await _shipOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.OrdersComplete)]
    public async Task<ActionResult<OrderResponse>> Complete(Guid id, CancellationToken cancellationToken) =>
        Ok(await _completeOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.OrdersCancel)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _cancelOrder.Handle(id, cancellationToken));

    [HttpPost("{id:guid}/fail")]
    [Authorize(Policy = Policies.OrdersFail)]
    public async Task<ActionResult<OrderResponse>> Fail(Guid id, CancellationToken cancellationToken) =>
        Ok(await _failOrder.Handle(id, cancellationToken));
}
