using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;
using OrderProcessing.Infrastructure.Messaging;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class OutboxApiTests
{
    private readonly SqlServerFixture _fixture;

    public OutboxApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubmitOrder_EnqueuesPendingOutboxMessage()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await (await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest("Outbox Buyer", $"{Guid.NewGuid():N}@example.com", "New", null)))
            .Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);

        var product = await (await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest($"O-{Guid.NewGuid():N}"[..12], "Outbox Item", 10m, null, "USD", 1)))
            .Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);

        var order = await (await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(customer!.Id, [new OrderItemRequest(product!.Id, 1)], null)))
            .Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);

        (await client.PostAsync($"/api/orders/{order!.Id}/submit", null)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderProcessingDbContext>();
        var outbox = await db.OutboxMessages
            .Where(message => message.EventType == "order.submitted" && message.PayloadJson.Contains(order.Id.ToString()))
            .OrderByDescending(message => message.OccurredAt)
            .FirstAsync();

        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.AttemptCount);
    }
}
