using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    [Authorize(Policy = Policies.ProductsRead)]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _listProducts.Handle(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ProductsRead)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await _getProduct.Handle(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.ProductsManage)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _createProduct.Handle(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ProductsManage)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _updateProduct.Handle(id, request, cancellationToken));
}
