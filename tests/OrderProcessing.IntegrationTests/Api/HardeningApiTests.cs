using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OrderProcessing.Application.Auth;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class HardeningApiTests
{
    private readonly SqlServerFixture _fixture;

    public HardeningApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Orders_FilterByStatus_ReturnsMatchingOnly()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, 5);
        var draft = await CreateDraft(client, customer.Id, product.Id, 1);
        await client.PostAsync($"/api/orders/{draft.Id}/submit", null);

        var pending = await client.GetFromJsonAsync<PagedResult<OrderResponse>>(
            "/api/orders?status=Pending&pageSize=100",
            ApiTestHelpers.JsonOptions);
        var drafts = await client.GetFromJsonAsync<PagedResult<OrderResponse>>(
            "/api/orders?status=Draft&pageSize=100",
            ApiTestHelpers.JsonOptions);

        Assert.Contains(pending!.Items, order => order.Id == draft.Id);
        Assert.DoesNotContain(drafts!.Items, order => order.Id == draft.Id);
    }

    [Fact]
    public async Task Products_SearchBySkuFragment_ReturnsMatch()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var marker = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sku = $"S{marker}";
        await CreateProduct(client, 1, sku);

        var page = await client.GetFromJsonAsync<PagedResult<ProductResponse>>(
            $"/api/products?search={marker}&pageSize=50",
            ApiTestHelpers.JsonOptions);

        Assert.Contains(page!.Items, product => product.Sku == sku);
    }

    [Fact]
    public async Task Sales_CannotConfirm_Returns403()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        await factory.EnsureSalesUserAsync();

        var admin = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        var sales = factory.CreateClient().WithBearer(
            await ApiTestHelpers.LoginAsync(factory.CreateClient(), "sales@localhost", "Sales_Pass_123!"));

        var customer = await CreateCustomer(admin);
        var product = await CreateProduct(admin, 3);
        var order = await CreateDraft(admin, customer.Id, product.Id, 1);
        await admin.PostAsync($"/api/orders/{order.Id}/submit", null);

        var confirm = await sales.PostAsync($"/api/orders/{order.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, confirm.StatusCode);
    }

    [Fact]
    public async Task DraftOrder_StaleConcurrencyToken_Returns409()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, 4);
        var created = await CreateDraft(client, customer.Id, product.Id, 1);

        var get = await client.GetAsync($"/api/orders/{created.Id}");
        var order = await get.Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(order?.ConcurrencyToken));
        Assert.True(get.Headers.ETag is not null);

        var firstUpdate = new UpdateDraftOrderRequest(
            [new OrderItemRequest(product.Id, 2)],
            "first",
            order!.ConcurrencyToken);
        var ok = await client.PutAsJsonAsync($"/api/orders/{created.Id}", firstUpdate);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var staleUpdate = new UpdateDraftOrderRequest(
            [new OrderItemRequest(product.Id, 1)],
            "stale",
            order.ConcurrencyToken);
        var conflict = await client.PutAsJsonAsync($"/api/orders/{created.Id}", staleUpdate);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task DraftOrder_IfMatchHeader_RejectsStaleETag()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, 4);
        var created = await CreateDraft(client, customer.Id, product.Id, 1);

        var get = await client.GetAsync($"/api/orders/{created.Id}");
        var order = await get.Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);
        var etag = get.Headers.ETag!.Tag;

        var body = new UpdateDraftOrderRequest([new OrderItemRequest(product.Id, 2)], "via-etag");
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{created.Id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        var first = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{created.Id}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);
        var conflict = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterLogout_Returns401()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@localhost", "Admin_Pass_123!"));
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(ApiTestHelpers.JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body?.RefreshToken));

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(body!.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(body.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    private static async Task<CustomerResponse> CreateCustomer(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest("Hardening Buyer", $"{Guid.NewGuid():N}@example.com", "New", null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions))!;
    }

    private static async Task<ProductResponse> CreateProduct(HttpClient client, int quantity, string? sku = null)
    {
        sku ??= $"H-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(sku, "Hardening Item", 10m, null, "USD", quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions))!;
    }

    private static async Task<OrderResponse> CreateDraft(HttpClient client, Guid customerId, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(customerId, [new OrderItemRequest(productId, quantity)], null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions))!;
    }
}
