using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;
using OrderProcessing.Infrastructure.Messaging;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("Messaging")]
public sealed class MessagingApiTests
{
    private readonly MessagingFixture _fixture;

    public MessagingApiTests(MessagingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubmitOrder_EventuallyPublishesOutboxAndConsumesIdempotently()
    {
        await using var factory = new ApiFactory(
            _fixture.ConnectionString,
            new MessagingTestSettings(true, _fixture.RabbitHost, _fixture.RabbitPort));

        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await (await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest("Messaging Buyer", $"{Guid.NewGuid():N}@example.com", "New", null)))
            .Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);

        var product = await (await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest($"M-{Guid.NewGuid():N}"[..12], "Msg Item", 10m, null, "USD", 3)))
            .Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);

        var order = await (await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(customer!.Id, [new OrderItemRequest(product!.Id, 1)], null)))
            .Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);

        var submit = await client.PostAsync($"/api/orders/{order!.Id}/submit", null);
        submit.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderProcessingDbContext>();

        var outboxId = await FindSubmittedOutboxIdAsync(db, order.Id);

        var outbox = await WaitForAsync(
            async () => await db.OutboxMessages.FirstOrDefaultAsync(message => message.Id == outboxId),
            message => message?.Status == OutboxMessageStatus.Published,
            timeout: TimeSpan.FromSeconds(30));

        Assert.NotNull(outbox);
        Assert.Equal(OutboxMessageStatus.Published, outbox!.Status);

        var processed = await WaitForAsync(
            async () => await db.ProcessedMessages.FirstOrDefaultAsync(message => message.MessageId == outboxId),
            message => message is not null,
            timeout: TimeSpan.FromSeconds(30));

        Assert.NotNull(processed);
        Assert.Equal("order.submitted", processed!.EventType);
    }

    private static async Task<Guid> FindSubmittedOutboxIdAsync(OrderProcessingDbContext db, Guid orderId)
    {
        var message = await db.OutboxMessages
            .Where(entry => entry.EventType == "order.submitted" && entry.PayloadJson.Contains(orderId.ToString()))
            .OrderByDescending(entry => entry.OccurredAt)
            .FirstAsync();

        return message.Id;
    }

    private static async Task<T?> WaitForAsync<T>(
        Func<Task<T?>> probe,
        Func<T?, bool> predicate,
        TimeSpan timeout)
        where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        T? current = null;

        while (DateTime.UtcNow < deadline)
        {
            current = await probe();
            if (predicate(current))
            {
                return current;
            }

            await Task.Delay(500);
        }

        return current;
    }
}
