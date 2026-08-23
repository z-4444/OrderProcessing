using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
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

    Task<PagedResult<Customer>> ListAsync(CustomerListQuery query, CancellationToken cancellationToken = default);

    void Add(Customer customer);
}

public interface IProductStore
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken cancellationToken = default);

    void Add(Product product);
}

public interface IOrderStore
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Order>> ListAsync(OrderListQuery query, CancellationToken cancellationToken = default);

    Task<int> CountCreatedOnUtcDateAsync(DateTime utcDate, CancellationToken cancellationToken = default);

    void Add(Order order);

    void RemoveItem(OrderItem item);
}

public interface IInventoryStore
{
    Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryItem>> GetByProductIdsAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryItem>> ListAsync(InventoryListQuery query, CancellationToken cancellationToken = default);

    void Add(InventoryItem item);

    void AddTransaction(InventoryTransaction transaction);
}

public interface IOrderAuditStore
{
    void Add(OrderAuditEvent auditEvent);
}

public interface IOutboxStore
{
    void Add(string eventType, string payloadJson, DateTimeOffset occurredAt, string? correlationId = null);
}

public interface IOrderNumberGenerator
{
    Task<OrderNumber> NextAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}

public sealed record CustomerListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    string? Segment = null);

public sealed record ProductListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null);

public sealed record OrderListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    Guid? CustomerId = null,
    string? OrderNumber = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record InventoryListQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? ProductId = null,
    bool? LowStockOnly = null);
