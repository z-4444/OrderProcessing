using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.IntegrationTests;

[Collection("SqlServer")]
public sealed class PersistenceTests
{
    private readonly SqlServerFixture _fixture;

    public PersistenceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DbContext_CanConnectToSqlServer()
    {
        await using var dbContext = _fixture.CreateContext();
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Migration_AppliesSuccessfully()
    {
        await using var dbContext = _fixture.CreateContext();
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();

        Assert.Empty(pending);
        Assert.Contains(applied, id => id.Contains("InitialPersistence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Customer_PersistsAndReloads()
    {
        await using var dbContext = _fixture.CreateContext();
        var customer = PersistenceTestHelpers.CreateCustomer("reload.customer@example.com");
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var reloaded = await dbContext.Customers.SingleAsync(item => item.Id == customer.Id);

        Assert.Equal("reload.customer@example.com", reloaded.Email);
        Assert.Equal("Test Customer", reloaded.Name);
        Assert.Equal(CustomerSegment.Loyal, reloaded.Segment);
        Assert.Equal(CustomerStatus.Active, reloaded.Status);
    }

    [Fact]
    public async Task Product_AndInventoryItem_PersistTogether()
    {
        await using var dbContext = _fixture.CreateContext();
        var product = PersistenceTestHelpers.CreateProduct(sku: $"INV-{Guid.NewGuid():N}"[..12]);
        var inventory = new InventoryItem(product.Id, quantityOnHand: 40);
        dbContext.Products.Add(product);
        dbContext.InventoryItems.Add(inventory);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var reloadedInventory = await dbContext.InventoryItems.SingleAsync(item => item.ProductId == product.Id);
        var reloadedProduct = await dbContext.Products.SingleAsync(item => item.Id == product.Id);

        Assert.Equal(40, reloadedInventory.QuantityOnHand);
        Assert.Equal(0, reloadedInventory.QuantityReserved);
        Assert.Equal(product.Sku.Value, reloadedProduct.Sku.Value);
    }

    [Fact]
    public async Task Order_WithMultipleItems_PersistsAndReloads()
    {
        await using var dbContext = _fixture.CreateContext();
        var customer = PersistenceTestHelpers.CreateCustomer();
        var first = PersistenceTestHelpers.CreateProduct(10m, name: "First");
        var second = PersistenceTestHelpers.CreateProduct(20m, name: "Second");
        dbContext.Customers.Add(customer);
        dbContext.Products.AddRange(first, second);
        dbContext.InventoryItems.AddRange(new InventoryItem(first.Id, 10), new InventoryItem(second.Id, 10));
        await dbContext.SaveChangesAsync();

        var order = PersistenceTestHelpers.CreateOrderWithItems(customer.Id, (first, 2), (second, 1));
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var reloaded = await dbContext.Orders.Include("_items").SingleAsync(item => item.Id == order.Id);

        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(OrderStatus.Draft, reloaded.Status);
        Assert.Contains(reloaded.Items, item => item.ProductName == "First" && item.Quantity == 2);
        Assert.Contains(reloaded.Items, item => item.ProductName == "Second" && item.Quantity == 1);
    }

    [Fact]
    public async Task OrderItemSnapshot_RemainsUnchanged_WhenProductPriceAndNameChange()
    {
        await using var dbContext = _fixture.CreateContext();
        var (customer, product, _) = await PersistenceTestHelpers.SeedCatalogAsync(dbContext, unitPrice: 25m);
        var order = PersistenceTestHelpers.CreateOrderWithItems(customer.Id, (product, 2));
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Products SET Name = 'Changed Name', UnitPrice = 999.00 WHERE Id = {product.Id}");

        dbContext.ChangeTracker.Clear();
        var reloaded = await dbContext.Orders.Include("_items").SingleAsync(item => item.Id == order.Id);
        var line = Assert.Single(reloaded.Items);

        Assert.Equal("Test Product", line.ProductName);
        Assert.Equal(25.00m, line.UnitPrice.Amount);
        Assert.Equal(50.00m, line.LineTotal.Amount);
    }

    [Fact]
    public async Task UniqueSku_IsEnforced()
    {
        await using var dbContext = _fixture.CreateContext();
        var sku = $"UNIQ-{Guid.NewGuid():N}"[..12];
        dbContext.Products.Add(PersistenceTestHelpers.CreateProduct(sku: sku, name: "One"));
        await dbContext.SaveChangesAsync();

        dbContext.Products.Add(PersistenceTestHelpers.CreateProduct(sku: sku, name: "Two"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.IsType<SqlException>(exception.InnerException);
    }

    [Fact]
    public async Task UniqueOrderNumber_IsEnforced()
    {
        await using var dbContext = _fixture.CreateContext();
        var (customer, product, _) = await PersistenceTestHelpers.SeedCatalogAsync(dbContext);
        var orderNumber = new OrderNumber($"ORD-UNIQ-{Guid.NewGuid().ToString("N")[..8]}");

        var first = Order.CreateDraft(customer.Id, CustomerSegment.New, orderNumber, PersistenceTestHelpers.Currency, 0.08m, DateTimeOffset.UtcNow);
        first.AddItem(product.Id, product.Sku, product.Name, 1, product.UnitPrice, Money.Zero(PersistenceTestHelpers.Currency));
        dbContext.Orders.Add(first);
        await dbContext.SaveChangesAsync();

        var second = Order.CreateDraft(customer.Id, CustomerSegment.New, orderNumber, PersistenceTestHelpers.Currency, 0.08m, DateTimeOffset.UtcNow);
        second.AddItem(product.Id, product.Sku, product.Name, 1, product.UnitPrice, Money.Zero(PersistenceTestHelpers.Currency));
        dbContext.Orders.Add(second);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        Assert.IsType<SqlException>(exception.InnerException);
    }

    [Fact]
    public async Task InventoryCheckConstraint_RejectsReservedAboveOnHand()
    {
        await using var dbContext = _fixture.CreateContext();
        var product = PersistenceTestHelpers.CreateProduct();
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InventoryItems (ProductId, QuantityOnHand, QuantityReserved) VALUES ({product.Id}, 5, 10)"));

        Assert.Equal(547, exception.Number);
    }

    [Fact]
    public async Task OrderRowVersion_DetectsConcurrencyConflict()
    {
        await using var setup = _fixture.CreateContext();
        var (customer, product, _) = await PersistenceTestHelpers.SeedCatalogAsync(setup);
        var order = PersistenceTestHelpers.CreateOrderWithItems(customer.Id, (product, 1));
        setup.Orders.Add(order);
        await setup.SaveChangesAsync();
        var orderId = order.Id;

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstOrder = await first.Orders.SingleAsync(item => item.Id == orderId);
        var secondOrder = await second.Orders.SingleAsync(item => item.Id == orderId);

        firstOrder.UpdateNotes("first writer");
        await first.SaveChangesAsync();

        secondOrder.UpdateNotes("second writer");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DecimalValues_PersistWithTwoPlacePrecision()
    {
        await using var dbContext = _fixture.CreateContext();
        var product = PersistenceTestHelpers.CreateProduct(unitPrice: 12.345m);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var reloaded = await dbContext.Products.SingleAsync(item => item.Id == product.Id);

        Assert.Equal(12.35m, reloaded.UnitPrice.Amount);
        Assert.Equal("USD", reloaded.UnitPrice.Currency);
    }

    [Fact]
    public async Task OrderStatus_PersistsAfterLifecycleTransition()
    {
        await using var dbContext = _fixture.CreateContext();
        var (customer, product, _) = await PersistenceTestHelpers.SeedCatalogAsync(dbContext, unitPrice: 120m);
        var order = PersistenceTestHelpers.CreateOrderWithItems(customer.Id, (product, 1));
        order.Submit(PersistenceTestHelpers.PricingCalculator);
        order.Confirm(PersistenceTestHelpers.PricingCalculator);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var reloaded = await dbContext.Orders.SingleAsync(item => item.Id == order.Id);

        Assert.Equal(OrderStatus.Confirmed, reloaded.Status);
        Assert.Equal(12.00m, reloaded.DiscountAmount.Amount);
    }
}
