using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Pricing;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class DevelopmentDataSeeder
{
    public const string Currency = "USD";
    private const decimal TaxRate = 0.08m;

    private readonly OrderProcessingDbContext _dbContext;

    public DevelopmentDataSeeder(OrderProcessingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var createdAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

        var customers = CreateCustomers(createdAt);
        var products = CreateProducts(createdAt);
        var inventory = products
            .Select(product => new InventoryItem(product.Id, quantityOnHand: 100))
            .ToList();

        var calculator = new OrderPricingCalculator(new LoyalCustomerDiscountPolicy());
        var sampleOrder = CreateSampleOrder(products, calculator, createdAt);

        await _dbContext.Customers.AddRangeAsync(customers, cancellationToken);
        await _dbContext.Products.AddRangeAsync(products, cancellationToken);
        await _dbContext.InventoryItems.AddRangeAsync(inventory, cancellationToken);
        await _dbContext.Orders.AddAsync(sampleOrder, cancellationToken);
        await _dbContext.OrderAuditEvents.AddAsync(
            OrderAuditEvent.Create(
                sampleOrder.Id,
                OrderAuditEventType.Created,
                createdAt,
                "Development seed order created."),
            cancellationToken);
        await _dbContext.InventoryTransactions.AddAsync(
            InventoryTransaction.Create(
                DevelopmentSeedIds.WidgetProductId,
                InventoryTransactionType.Adjustment,
                quantity: 100,
                createdAt,
                reason: "Initial stock"),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<Customer> CreateCustomers(DateTimeOffset createdAt) =>
    [
        Customer.Create(
            DevelopmentSeedIds.AliceCustomerId,
            "Alice Nguyen",
            "alice.nguyen@example.com",
            CustomerSegment.New,
            createdAt,
            "555-0101"),
        Customer.Create(
            DevelopmentSeedIds.BobCustomerId,
            "Bob Martinez",
            "bob.martinez@example.com",
            CustomerSegment.Loyal,
            createdAt,
            "555-0102"),
        Customer.Create(
            DevelopmentSeedIds.CarolCustomerId,
            "Carol Singh",
            "carol.singh@example.com",
            CustomerSegment.Loyal,
            createdAt,
            "555-0103")
    ];

    private static List<Product> CreateProducts(DateTimeOffset createdAt) =>
    [
        Product.Create(DevelopmentSeedIds.WidgetProductId, new Sku("WID-001"), "Standard Widget", new Money(25.00m, Currency), createdAt, "General-purpose widget"),
        Product.Create(DevelopmentSeedIds.GadgetProductId, new Sku("GAD-002"), "Super Gadget", new Money(80.00m, Currency), createdAt, "Premium gadget"),
        Product.Create(DevelopmentSeedIds.CableProductId, new Sku("CBL-003"), "USB-C Cable", new Money(12.50m, Currency), createdAt),
        Product.Create(DevelopmentSeedIds.ChairProductId, new Sku("CHR-004"), "Office Chair", new Money(150.00m, Currency), createdAt),
        Product.Create(DevelopmentSeedIds.LampProductId, new Sku("LMP-005"), "Desk Lamp", new Money(45.00m, Currency), createdAt),
        Product.Create(DevelopmentSeedIds.MouseProductId, new Sku("MOU-006"), "Wireless Mouse", new Money(35.00m, Currency), createdAt),
        Product.Create(DevelopmentSeedIds.KeyboardProductId, new Sku("KEY-007"), "Mechanical Keyboard", new Money(99.99m, Currency), createdAt),
        Product.Create(DevelopmentSeedIds.MonitorProductId, new Sku("MON-008"), "24-inch Monitor", new Money(220.00m, Currency), createdAt)
    ];

    private static Order CreateSampleOrder(
        IReadOnlyList<Product> products,
        OrderPricingCalculator calculator,
        DateTimeOffset createdAt)
    {
        var widget = products.Single(product => product.Id == DevelopmentSeedIds.WidgetProductId);
        var lamp = products.Single(product => product.Id == DevelopmentSeedIds.LampProductId);

        var order = Order.CreateDraft(
            DevelopmentSeedIds.BobCustomerId,
            CustomerSegment.Loyal,
            new OrderNumber("ORD-20260801-0001"),
            Currency,
            TaxRate,
            createdAt,
            "Seeded development order");

        order.AddItem(widget.Id, widget.Sku, widget.Name, 4, widget.UnitPrice, Money.Zero(Currency));
        order.AddItem(lamp.Id, lamp.Sku, lamp.Name, 1, lamp.UnitPrice, Money.Zero(Currency));
        order.Submit(calculator);
        return order;
    }
}
