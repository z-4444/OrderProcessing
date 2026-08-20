using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Common;
using OrderProcessing.Domain.Customers;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .HasConversion(number => number.Value, value => new OrderNumber(value))
            .HasColumnName("OrderNumber")
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique();

        builder.HasIndex(order => new { order.Status, order.CreatedAt });
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt });

        builder.Property(order => order.CustomerSegment)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(order => order.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(order => order.TaxRate)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(order => order.Notes)
            .HasMaxLength(2000);

        builder.Property(order => order.CreatedAt).IsRequired();
        builder.Property(order => order.UpdatedAt).IsRequired();

        ConfigureMoney(builder, order => order.Subtotal, "Subtotal");
        ConfigureMoney(builder, order => order.DiscountAmount, "DiscountAmount");
        ConfigureMoney(builder, order => order.TaxAmount, "TaxAmount");
        ConfigureMoney(builder, order => order.GrandTotal, "GrandTotal");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<OrderItem>("_items")
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        builder.Ignore(order => order.Items);
        builder.Ignore(order => order.CanCancel);
        builder.Ignore(order => order.IsEditable);

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }

    private static void ConfigureMoney(
        EntityTypeBuilder<Order> builder,
        System.Linq.Expressions.Expression<Func<Order, Money>> property,
        string columnPrefix)
    {
        builder.ComplexProperty(property, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName(columnPrefix)
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName($"{columnPrefix}Currency")
                .HasMaxLength(3)
                .IsRequired();
        });
    }
}
