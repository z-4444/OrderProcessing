using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;
using OrderProcessing.Infrastructure.Identity;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class OrderProcessingDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public OrderProcessingDbContext(DbContextOptions<OrderProcessingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderAuditEvent> OrderAuditEvents => Set<OrderAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderProcessingDbContext).Assembly);
    }
}
