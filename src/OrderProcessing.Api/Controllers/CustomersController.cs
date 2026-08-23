using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.Customers;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly CreateCustomer _createCustomer;
    private readonly UpdateCustomer _updateCustomer;
    private readonly GetCustomer _getCustomer;
    private readonly ListCustomers _listCustomers;

    public CustomersController(
        CreateCustomer createCustomer,
        UpdateCustomer updateCustomer,
        GetCustomer getCustomer,
        ListCustomers listCustomers)
    {
        _createCustomer = createCustomer;
        _updateCustomer = updateCustomer;
        _getCustomer = getCustomer;
        _listCustomers = listCustomers;
    }

    /// <summary>
    /// Lists customers. Filter with search (name/email), status, and segment.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CustomersRead)]
    [ProducesResponseType(typeof(PagedResult<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> List(
        [FromQuery] CustomerListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _listCustomers.Handle(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CustomersRead)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _getCustomer.Handle(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.CustomersWrite)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _createCustomer.Handle(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CustomersWrite)]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _updateCustomer.Handle(id, request, cancellationToken));
}
