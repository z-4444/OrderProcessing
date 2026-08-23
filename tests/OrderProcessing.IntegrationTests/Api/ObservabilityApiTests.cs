using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;
using OrderProcessing.Infrastructure.Persistence;
using OrderProcessing.Api.Middleware;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class ObservabilityApiTests
{
    private readonly SqlServerFixture _fixture;

    public ObservabilityApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthyJson()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthyJson_WhenDatabaseAvailable()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("database", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoints_DoNotRequireAuthentication()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Request_WithoutCorrelationHeader_ReceivesGeneratedCorrelationId()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.First()));
    }

    [Fact]
    public async Task Request_WithCorrelationHeader_EchoesSameValue()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        const string correlationId = "test-correlation-abc123";

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.Equal(correlationId, values!.First());
    }

    [Fact]
    public async Task NotFound_ErrorIncludesCorrelationIdExtension()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        const string correlationId = "error-correlation-xyz";

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{Guid.NewGuid()}");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ApiTestHelpers.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(json.TryGetProperty("correlationId", out var extension));
        Assert.Equal(correlationId, extension.GetString());
    }

    [Fact]
    public async Task SubmitOrder_PersistsOutboxWithRequestCorrelationId()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        const string correlationId = "outbox-correlation-001";

        var customer = await (await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest("Obs Buyer", $"{Guid.NewGuid():N}@example.com", "New", null)))
            .Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);

        var product = await (await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest($"O-{Guid.NewGuid():N}"[..12], "Obs Item", 10m, null, "USD", 5)))
            .Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);

        var order = await (await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(customer!.Id, [new OrderItemRequest(product!.Id, 1)], null)))
            .Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);

        var submit = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order!.Id}/submit");
        submit.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        var submitResponse = await client.SendAsync(submit);
        submitResponse.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderProcessingDbContext>();
        var outbox = await db.OutboxMessages
            .Where(message => message.EventType == "order.submitted")
            .OrderByDescending(message => message.OccurredAt)
            .FirstAsync();

        Assert.Equal(correlationId, outbox.CorrelationId);
        Assert.Contains(correlationId, outbox.PayloadJson, StringComparison.Ordinal);
    }
}
