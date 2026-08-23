using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace OrderProcessing.IntegrationTests;

public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Test_Password_123!")
        .Build();

    private readonly RabbitMqContainer _rabbitContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public string RabbitHost { get; private set; } = string.Empty;

    public int RabbitPort { get; private set; }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _rabbitContainer.StartAsync());

        var builder = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = "OrderProcessingMessagingTests"
        };
        ConnectionString = builder.ConnectionString;

        RabbitHost = _rabbitContainer.Hostname;
        RabbitPort = _rabbitContainer.GetMappedPublicPort(5672);

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
        await _sqlContainer.DisposeAsync().AsTask();
        await _rabbitContainer.DisposeAsync().AsTask();
    }
}

[CollectionDefinition("Messaging")]
public sealed class MessagingCollection : ICollectionFixture<MessagingFixture>
{
}
