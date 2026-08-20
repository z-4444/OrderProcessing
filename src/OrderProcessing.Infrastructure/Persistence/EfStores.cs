using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Infrastructure.Persistence;

internal sealed class EfApplicationPersistence : IApplicationPersistence
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfApplicationPersistence(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            foreach (var entry in exception.Entries)
            {
                if (entry.Entity is OrderItem)
                {
                    entry.State = EntityState.Detached;
                    continue;
                }

                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues is null)
                {
                    throw new ConflictException("The record was deleted by another user.");
                }

                entry.OriginalValues.SetValues(databaseValues);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }
}

internal sealed class EfCustomerStore : ICustomerStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfCustomerStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Email == normalized, cancellationToken);
    }

    public async Task<PagedResult<Customer>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Customers.AsNoTracking().OrderBy(customer => customer.Name);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Customer>(items, page, pageSize, total);
    }

    public void Add(Customer customer) => _dbContext.Customers.Add(customer);
}

internal sealed class EfProductStore : IProductStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfProductStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var skuValue = new Sku(sku);
        return _dbContext.Products.FirstOrDefaultAsync(product => product.Sku == skuValue, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => idList.Contains(product.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Product>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products.AsNoTracking().OrderBy(product => product.Name);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Product>(items, page, pageSize, total);
    }

    public void Add(Product product) => _dbContext.Products.Add(product);
}

internal sealed class EfOrderStore : IOrderStore
{
    private readonly OrderProcessingDbContext _dbContext;

    public EfOrderStore(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .Include("_items")
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<PagedResult<Order>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking().OrderByDescending(order => order.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Order>(items, page, pageSize, total);
    }

    public Task<int> CountCreatedOnUtcDateAsync(DateTime utcDate, CancellationToken cancellationToken = default)
    {
        var start = new DateTimeOffset(utcDate.Date, TimeSpan.Zero);
        var end = start.AddDays(1);
        return _dbContext.Orders.CountAsync(order => order.CreatedAt >= start && order.CreatedAt < end, cancellationToken);
    }

    public void Add(Order order) => _dbContext.Orders.Add(order);

    public void RemoveItem(OrderItem item) => _dbContext.Set<OrderItem>().Remove(item);
}

internal sealed class EfOrderNumberGenerator : IOrderNumberGenerator
{
    private readonly IOrderStore _orders;

    public EfOrderNumberGenerator(IOrderStore orders)
    {
        _orders = orders;
    }

    public async Task<OrderNumber> NextAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var count = await _orders.CountCreatedOnUtcDateAsync(utcNow.UtcDateTime.Date, cancellationToken);
        return OrderNumber.Create(utcNow, count + 1);
    }
}
