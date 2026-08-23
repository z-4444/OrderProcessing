using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderProcessing.Application.Auth;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Inventory;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class OrderLifecycleApiTests
{
    private readonly SqlServerFixture _fixture;

    public OrderLifecycleApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Lifecycle_SubmitConfirmProcessShipComplete_UpdatesInventoryAndStatus()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, quantity: 10);
        var order = await CreateDraft(client, customer.Id, product.Id, quantity: 2);

        order = await PostLifecycle(client, order.Id, "submit", "Pending");
        order = await PostLifecycle(client, order.Id, "confirm", "Confirmed");

        var inventoryAfterConfirm = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/api/inventory/{product.Id}",
            ApiTestHelpers.JsonOptions);
        Assert.Equal(2, inventoryAfterConfirm!.QuantityReserved);
        Assert.Equal(8, inventoryAfterConfirm.AvailableQuantity);

        order = await PostLifecycle(client, order.Id, "process", "Processing");
        order = await PostLifecycle(client, order.Id, "ship", "Shipped");

        var inventoryAfterShip = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/api/inventory/{product.Id}",
            ApiTestHelpers.JsonOptions);
        Assert.Equal(0, inventoryAfterShip!.QuantityReserved);
        Assert.Equal(8, inventoryAfterShip.QuantityOnHand);

        order = await PostLifecycle(client, order.Id, "complete", "Completed");
        Assert.Equal("Completed", order.Status);
    }

    [Fact]
    public async Task Confirm_WhenInsufficientStock_ReturnsConflict()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, quantity: 1);
        var order = await CreateDraft(client, customer.Id, product.Id, quantity: 5);
        await PostLifecycle(client, order.Id, "submit", "Pending");

        var confirm = await client.PostAsync($"/api/orders/{order.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_ReleasesInventory()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await CreateCustomer(client);
        var product = await CreateProduct(client, quantity: 5);
        var order = await CreateDraft(client, customer.Id, product.Id, quantity: 3);
        await PostLifecycle(client, order.Id, "submit", "Pending");
        await PostLifecycle(client, order.Id, "confirm", "Confirmed");
        await PostLifecycle(client, order.Id, "cancel", "Cancelled");

        var inventory = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/api/inventory/{product.Id}",
            ApiTestHelpers.JsonOptions);
        Assert.Equal(0, inventory!.QuantityReserved);
        Assert.Equal(5, inventory.QuantityOnHand);
    }

    [Fact]
    public async Task Warehouse_CannotConfirm_ButCanShip()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        await factory.EnsureWarehouseUserAsync();
        var admin = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        var warehouse = factory.CreateClient().WithBearer(
            await ApiTestHelpers.LoginAsync(factory.CreateClient(), "warehouse@localhost", "Warehouse_Pass_123!"));

        var customer = await CreateCustomer(admin);
        var product = await CreateProduct(admin, quantity: 4);
        var order = await CreateDraft(admin, customer.Id, product.Id, quantity: 1);
        await PostLifecycle(admin, order.Id, "submit", "Pending");

        var forbidden = await warehouse.PostAsync($"/api/orders/{order.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await PostLifecycle(admin, order.Id, "confirm", "Confirmed");
        await PostLifecycle(warehouse, order.Id, "process", "Processing");
        await PostLifecycle(warehouse, order.Id, "ship", "Shipped");
    }

    [Fact]
    public async Task RefreshToken_RotatesAccessToken()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@localhost", "Admin_Pass_123!"));
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(ApiTestHelpers.JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body?.RefreshToken));

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(body!.RefreshToken));
        var refreshed = await refresh.Content.ReadFromJsonAsync<LoginResponse>(ApiTestHelpers.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshed?.AccessToken));
        Assert.NotEqual(body.RefreshToken, refreshed!.RefreshToken);
    }

    private static async Task<CustomerResponse> CreateCustomer(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest("Lifecycle Buyer", $"{Guid.NewGuid():N}@example.com", "Loyal", null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions))!;
    }

    private static async Task<ProductResponse> CreateProduct(HttpClient client, int quantity)
    {
        var sku = $"L-{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(sku, "Lifecycle Item", 25m, null, "USD", quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions))!;
    }

    private static async Task<OrderResponse> CreateDraft(HttpClient client, Guid customerId, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(customerId, [new OrderItemRequest(productId, quantity)], null));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        return JsonSerializer.Deserialize<OrderResponse>(payload, ApiTestHelpers.JsonOptions)!;
    }

    private static async Task<OrderResponse> PostLifecycle(HttpClient client, Guid orderId, string action, string expectedStatus)
    {
        var response = await client.PostAsync($"/api/orders/{orderId}/{action}", null);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{action} failed: {(int)response.StatusCode} {payload}");
        var order = JsonSerializer.Deserialize<OrderResponse>(payload, ApiTestHelpers.JsonOptions)!;
        Assert.Equal(expectedStatus, order.Status);
        return order;
    }
}
