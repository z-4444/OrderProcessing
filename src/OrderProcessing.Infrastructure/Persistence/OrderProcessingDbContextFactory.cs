using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class OrderProcessingDbContextFactory : IDesignTimeDbContextFactory<OrderProcessingDbContext>
{
    public OrderProcessingDbContext CreateDbContext(string[] args)
    {
        var password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "Dev_Password_123!";
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__OrderProcessing")
            ?? $"Server=localhost,1433;Database=OrderProcessing;User Id=sa;Password={password};TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<OrderProcessingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OrderProcessingDbContext(options);
    }
}
