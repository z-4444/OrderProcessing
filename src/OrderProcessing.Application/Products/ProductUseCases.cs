using FluentValidation;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Application.Products;

public sealed class CreateProduct
{
    private readonly IProductStore _products;
    private readonly IApplicationPersistence _persistence;
    private readonly IValidator<CreateProductRequest> _validator;

    public CreateProduct(
        IProductStore products,
        IApplicationPersistence persistence,
        IValidator<CreateProductRequest> validator)
    {
        _products = products;
        _persistence = persistence;
        _validator = validator;
    }

    public async Task<ProductResponse> Handle(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var sku = new Sku(request.Sku);
        if (await _products.GetBySkuAsync(sku.Value, cancellationToken) is not null)
        {
            throw new ConflictException("A product with this SKU already exists.");
        }

        var product = Product.Create(
            Guid.NewGuid(),
            sku,
            request.Name,
            new Money(request.UnitPrice, request.Currency),
            DateTimeOffset.UtcNow,
            request.Description);

        _products.Add(product);
        await _persistence.SaveChangesAsync(cancellationToken);
        return product.ToResponse();
    }
}

public sealed class UpdateProduct
{
    private readonly IProductStore _products;
    private readonly IApplicationPersistence _persistence;
    private readonly IValidator<UpdateProductRequest> _validator;

    public UpdateProduct(
        IProductStore products,
        IApplicationPersistence persistence,
        IValidator<UpdateProductRequest> validator)
    {
        _products = products;
        _persistence = persistence;
        _validator = validator;
    }

    public async Task<ProductResponse> Handle(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var product = await _products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        product.Update(
            request.Name,
            new Money(request.UnitPrice, request.Currency),
            DateTimeOffset.UtcNow,
            request.Description,
            request.IsActive);

        await _persistence.SaveChangesAsync(cancellationToken);
        return product.ToResponse();
    }
}

public sealed class GetProduct
{
    private readonly IProductStore _products;

    public GetProduct(IProductStore products)
    {
        _products = products;
    }

    public async Task<ProductResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' was not found.");

        return product.ToResponse();
    }
}

public sealed class ListProducts
{
    private readonly IProductStore _products;

    public ListProducts(IProductStore products)
    {
        _products = products;
    }

    public async Task<PagedResult<ProductResponse>> Handle(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var result = await _products.ListAsync(page, pageSize, cancellationToken);
        return new PagedResult<ProductResponse>(
            result.Items.Select(item => item.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
