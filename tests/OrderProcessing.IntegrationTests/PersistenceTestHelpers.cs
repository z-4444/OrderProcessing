using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.IntegrationTests;

internal static class PersistenceTestHelpers
{
    public const string Currency = "USD";

    public static readonly OrderPricingCalculator PricingCalculator =
        new(new LoyalCustomerDiscountPolicy());

    public static Customer CreateCustomer(string? email = null)
    {
        return Customer.Create(
            Guid.NewGuid(),
            "Test Customer",
            email ?? $"{Guid.NewGuid():N}@example.com",
            CustomerSegment.Loyal,
            DateTimeOffset.UtcNow,
            "555-0000");
    }

    public static Product CreateProduct(
        decimal unitPrice = 25m,
        string? sku = null,
        string name = "Test Product")
    {
        var skuValue = sku ?? $"SKU-{Guid.NewGuid():N}"[..20];
        return Product.Create(
            Guid.NewGuid(),
            new Sku(skuValue),
            name,
            new Money(unitPrice, Currency),
            DateTimeOffset.UtcNow,
            "Test product");
    }

    public static async Task<(Customer Customer, Product Product, InventoryItem Inventory)> SeedCatalogAsync(
        OrderProcessingDbContext dbContext,
        decimal unitPrice = 25m,
        int quantityOnHand = 50)
    {
        var customer = CreateCustomer();
        var product = CreateProduct(unitPrice);
        var inventory = new InventoryItem(product.Id, quantityOnHand);

        dbContext.Customers.Add(customer);
        dbContext.Products.Add(product);
        dbContext.InventoryItems.Add(inventory);
        await dbContext.SaveChangesAsync();

        return (customer, product, inventory);
    }

    public static Order CreateOrderWithItems(
        Guid customerId,
        params (Product Product, int Quantity)[] lines)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var order = Order.CreateDraft(
            customerId,
            CustomerSegment.Loyal,
            new OrderNumber($"ORD-{DateTime.UtcNow:yyyyMMdd}-{suffix}"),
            Currency,
            taxRate: 0.08m,
            DateTimeOffset.UtcNow);

        foreach (var (product, quantity) in lines)
        {
            order.AddItem(
                product.Id,
                product.Sku,
                product.Name,
                quantity,
                product.UnitPrice,
                Money.Zero(Currency));
        }

        return order;
    }
}
