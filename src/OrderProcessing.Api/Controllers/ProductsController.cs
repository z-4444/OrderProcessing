using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Api.Concurrency;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.Products;

namespace OrderProcessing.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly CreateProduct _createProduct;
    private readonly UpdateProduct _updateProduct;
    private readonly GetProduct _getProduct;
    private readonly ListProducts _listProducts;

    public ProductsController(
        CreateProduct createProduct,
        UpdateProduct updateProduct,
        GetProduct getProduct,
        ListProducts listProducts)
    {
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _getProduct = getProduct;
        _listProducts = listProducts;
    }

    /// <summary>
    /// Lists products. Filter with search (name/SKU) and isActive.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.ProductsRead)]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] ProductListQuery query,
        CancellationToken cancellationToken = default) =>
        Ok(await _listProducts.Handle(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ProductsRead)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var product = await _getProduct.Handle(id, cancellationToken);
        ApplyETag(product.ConcurrencyToken);
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ProductsManage)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _createProduct.Handle(request, cancellationToken);
        ApplyETag(created.ConcurrencyToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates a product. Send If-Match with the ETag from GET, or concurrencyToken in the body.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ProductsManage)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        var updated = await _updateProduct.Handle(
            id,
            request,
            ConcurrencyHeader.Resolve(ifMatch, request.ConcurrencyToken),
            cancellationToken);
        ApplyETag(updated.ConcurrencyToken);
        return Ok(updated);
    }

    private void ApplyETag(string? concurrencyToken)
    {
        if (!string.IsNullOrWhiteSpace(concurrencyToken))
        {
            Response.Headers.ETag = ConcurrencyHeader.ToETag(concurrencyToken);
        }
    }
}
