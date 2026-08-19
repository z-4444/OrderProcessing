using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderProcessing")
            ?? throw new InvalidOperationException("Connection string 'OrderProcessing' is not configured.");

        services.AddDbContext<OrderProcessingDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<DevelopmentDataSeeder>();
        return services;
    }
}
