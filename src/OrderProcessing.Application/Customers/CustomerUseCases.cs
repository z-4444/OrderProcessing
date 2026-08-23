using FluentValidation;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Common;
using OrderProcessing.Domain.Customers;

namespace OrderProcessing.Application.Customers;

public sealed class CreateCustomer
{
    private readonly ICustomerStore _customers;
    private readonly IApplicationPersistence _persistence;
    private readonly IValidator<CreateCustomerRequest> _validator;

    public CreateCustomer(
        ICustomerStore customers,
        IApplicationPersistence persistence,
        IValidator<CreateCustomerRequest> validator)
    {
        _customers = customers;
        _persistence = persistence;
        _validator = validator;
    }

    public async Task<CustomerResponse> Handle(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await _customers.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("A customer with this email already exists.");
        }

        var segment = Enum.Parse<CustomerSegment>(request.Segment, ignoreCase: true);
        var customer = Customer.Create(
            Guid.NewGuid(),
            request.Name,
            request.Email,
            segment,
            DateTimeOffset.UtcNow,
            request.Phone);

        _customers.Add(customer);
        await _persistence.SaveChangesAsync(cancellationToken);
        return customer.ToResponse();
    }
}

public sealed class UpdateCustomer
{
    private readonly ICustomerStore _customers;
    private readonly IApplicationPersistence _persistence;
    private readonly IValidator<UpdateCustomerRequest> _validator;

    public UpdateCustomer(
        ICustomerStore customers,
        IApplicationPersistence persistence,
        IValidator<UpdateCustomerRequest> validator)
    {
        _customers = customers;
        _persistence = persistence;
        _validator = validator;
    }

    public async Task<CustomerResponse> Handle(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _customers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{id}' was not found.");

        var emailOwner = await _customers.GetByEmailAsync(request.Email, cancellationToken);
        if (emailOwner is not null && emailOwner.Id != id)
        {
            throw new ConflictException("A customer with this email already exists.");
        }

        customer.Update(
            request.Name,
            request.Email,
            Enum.Parse<CustomerSegment>(request.Segment, ignoreCase: true),
            Enum.Parse<CustomerStatus>(request.Status, ignoreCase: true),
            DateTimeOffset.UtcNow,
            request.Phone);

        await _persistence.SaveChangesAsync(cancellationToken);
        return customer.ToResponse();
    }
}

public sealed class GetCustomer
{
    private readonly ICustomerStore _customers;

    public GetCustomer(ICustomerStore customers)
    {
        _customers = customers;
    }

    public async Task<CustomerResponse> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{id}' was not found.");

        return customer.ToResponse();
    }
}

public sealed class ListCustomers
{
    private readonly ICustomerStore _customers;

    public ListCustomers(ICustomerStore customers)
    {
        _customers = customers;
    }

    public Task<PagedResult<CustomerResponse>> Handle(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        return Map(_customers.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken));
    }

    private static async Task<PagedResult<CustomerResponse>> Map(Task<PagedResult<Customer>> source)
    {
        var result = await source;
        return new PagedResult<CustomerResponse>(
            result.Items.Select(item => item.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
