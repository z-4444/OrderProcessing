using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace OrderProcessing.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Test_Password_123!")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "OrderProcessingTests"
        };
        ConnectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public OrderProcessingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrderProcessingDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new OrderProcessingDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
