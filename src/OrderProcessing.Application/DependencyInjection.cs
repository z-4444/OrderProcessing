using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Auth;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Inventory;
using OrderProcessing.Application.Observability;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;
using OrderProcessing.Domain.Pricing;

namespace OrderProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddSingleton<ICustomerDiscountPolicy, LoyalCustomerDiscountPolicy>();
        services.AddSingleton<OrderPricingCalculator>();

        services.AddScoped<Login>();
        services.AddScoped<RefreshAccessToken>();
        services.AddScoped<Logout>();
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
        services.AddScoped<SubmitOrder>();
        services.AddScoped<ConfirmOrder>();
        services.AddScoped<StartProcessingOrder>();
        services.AddScoped<ShipOrder>();
        services.AddScoped<CompleteOrder>();
        services.AddScoped<CancelOrder>();
        services.AddScoped<FailOrder>();
        services.AddScoped<GetInventory>();
        services.AddScoped<ListInventory>();
        services.AddScoped<AdjustInventory>();

        return services;
    }
}
