using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Auth;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;
using OrderProcessing.Domain.Pricing;

namespace OrderProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton<ICustomerDiscountPolicy, LoyalCustomerDiscountPolicy>();
        services.AddSingleton<OrderPricingCalculator>();

        services.AddScoped<Login>();
        services.AddScoped<GetCurrentUser>();
        services.AddScoped<CreateCustomer>();
        services.AddScoped<UpdateCustomer>();
        services.AddScoped<GetCustomer>();
        services.AddScoped<ListCustomers>();
        services.AddScoped<CreateProduct>();
        services.AddScoped<UpdateProduct>();
        services.AddScoped<GetProduct>();
        services.AddScoped<ListProducts>();
        services.AddScoped<CreateOrder>();
        services.AddScoped<UpdateDraftOrder>();
        services.AddScoped<GetOrder>();
        services.AddScoped<ListOrders>();

        return services;
    }
}
