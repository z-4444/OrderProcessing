using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Application.Abstractions;

public interface IApplicationPersistence
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICustomerStore
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PagedResult<Customer>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(Customer customer);
}

public interface IProductStore
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(Product product);
}

public interface IOrderStore
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Order>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountCreatedOnUtcDateAsync(DateTime utcDate, CancellationToken cancellationToken = default);

    void Add(Order order);

    void RemoveItem(OrderItem item);
}

public interface IOrderNumberGenerator
{
    Task<OrderNumber> NextAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
