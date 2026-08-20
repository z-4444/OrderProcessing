using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderProcessing.Application.Customers;
using OrderProcessing.Application.Orders;
using OrderProcessing.Application.Products;

namespace OrderProcessing.IntegrationTests.Api;

[Collection("SqlServer")]
public sealed class CustomerProductOrderApiTests
{
    private readonly SqlServerFixture _fixture;

    public CustomerProductOrderApiTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Customer_CreateGetUpdate_Works_AndInvalidEmailFails()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var email = $"{Guid.NewGuid():N}@example.com";
        var createdResponse = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("Ada Lovelace", email, "Loyal", "555-0100"));
        var created = await createdResponse.Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal("Ada Lovelace", created?.Name);

        var getResponse = await client.GetAsync($"/api/customers/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updatedResponse = await client.PutAsJsonAsync($"/api/customers/{created.Id}", new UpdateCustomerRequest("Ada L.", email, "Loyal", "Active", "555-0101"));
        var updated = await updatedResponse.Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);
        Assert.Equal("Ada L.", updated?.Name);

        var invalid = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("Bad", "not-an-email", "New", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("Copy", email, "New", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Product_CreateRetrieveUpdate_AndDuplicateSkuRejected()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));
        var sku = $"SKU-{Guid.NewGuid():N}"[..12];

        var createdResponse = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(sku, "Widget", 12.50m, "A widget"));
        var created = await createdResponse.Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/products/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updatedResponse = await client.PutAsJsonAsync($"/api/products/{created.Id}", new UpdateProductRequest("Widget+", 13.00m, true, "Updated"));
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);
        Assert.Equal(13.00m, updated?.UnitPrice);

        var duplicate = await client.PostAsJsonAsync("/api/products", new CreateProductRequest(sku, "Other", 1m, null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalidPrice = await client.PostAsJsonAsync("/api/products", new CreateProductRequest($"X-{Guid.NewGuid():N}"[..10], "Bad", 0m, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrice.StatusCode);
    }

    [Fact]
    public async Task DraftOrder_CreateAndUpdate_SnapshotsAndTotals()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        var client = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var customer = await (await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("Buyer", $"{Guid.NewGuid():N}@example.com", "Loyal", null)))
            .Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);
        var product = await (await client.PostAsJsonAsync("/api/products", new CreateProductRequest($"P-{Guid.NewGuid():N}"[..12], "Chair", 100m, null)))
            .Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);

        var createResponse = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            customer!.Id,
            [new OrderItemRequest(product!.Id, 2)],
            "first draft"));
        var createPayload = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode, $"Create order failed: {(int)createResponse.StatusCode} {createPayload}");
        var created = JsonSerializer.Deserialize<OrderResponse>(createPayload, ApiTestHelpers.JsonOptions);

        Assert.Equal("Draft", created!.Status);
        Assert.Equal("Chair", created.Items[0].ProductName);
        Assert.Equal(100m, created.Items[0].UnitPrice);
        Assert.Equal(200m, created.Subtotal);
        Assert.Equal(20m, created.Discount);
        Assert.Equal(14.40m, created.Tax);
        Assert.Equal(194.40m, created.GrandTotal);

        await client.PutAsJsonAsync($"/api/products/{product.Id}", new UpdateProductRequest("Chair renamed", 999m, true, null));

        var getResponse = await client.GetAsync($"/api/orders/{created.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<OrderResponse>(ApiTestHelpers.JsonOptions);
        Assert.Equal("Chair", reloaded!.Items[0].ProductName);
        Assert.Equal(100m, reloaded.Items[0].UnitPrice);

        var updateResponse = await client.PutAsJsonAsync($"/api/orders/{created.Id}", new UpdateDraftOrderRequest(
            [new OrderItemRequest(product.Id, 1)],
            "updated"));
        var updatePayload = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.IsSuccessStatusCode, $"Update order failed: {(int)updateResponse.StatusCode} {updatePayload}");
        var updated = JsonSerializer.Deserialize<OrderResponse>(updatePayload, ApiTestHelpers.JsonOptions);
        Assert.Single(updated!.Items);
        Assert.Equal(999m, updated.Items[0].UnitPrice);
    }

    [Fact]
    public async Task DraftOrder_RejectsUnknownCustomerInactiveProductAndUnauthorizedAccess()
    {
        await using var factory = new ApiFactory(_fixture.ConnectionString);
        await factory.EnsureWarehouseUserAsync();
        var adminClient = factory.CreateClient().WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient()));

        var missingCustomer = await adminClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            Guid.NewGuid(),
            [new OrderItemRequest(Guid.NewGuid(), 1)],
            null));
        Assert.Equal(HttpStatusCode.NotFound, missingCustomer.StatusCode);

        var customer = await (await adminClient.PostAsJsonAsync("/api/customers", new CreateCustomerRequest("Inactive Buyer", $"{Guid.NewGuid():N}@example.com", "New", null)))
            .Content.ReadFromJsonAsync<CustomerResponse>(ApiTestHelpers.JsonOptions);
        var product = await (await adminClient.PostAsJsonAsync("/api/products", new CreateProductRequest($"Z-{Guid.NewGuid():N}"[..12], "Lamp", 40m, null)))
            .Content.ReadFromJsonAsync<ProductResponse>(ApiTestHelpers.JsonOptions);
        await adminClient.PutAsJsonAsync($"/api/products/{product!.Id}", new UpdateProductRequest("Lamp", 40m, false, null));

        var inactiveProduct = await adminClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            customer!.Id,
            [new OrderItemRequest(product.Id, 1)],
            null));
        Assert.Equal(HttpStatusCode.Conflict, inactiveProduct.StatusCode);

        var anonymous = factory.CreateClient();
        var unauthenticated = await anonymous.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var warehouseClient = factory.CreateClient()
            .WithBearer(await ApiTestHelpers.LoginAsync(factory.CreateClient(), "warehouse@localhost", "Warehouse_Pass_123!"));
        var forbidden = await warehouseClient.PostAsJsonAsync("/api/products", new CreateProductRequest($"W-{Guid.NewGuid():N}"[..12], "Nope", 5m, null));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
