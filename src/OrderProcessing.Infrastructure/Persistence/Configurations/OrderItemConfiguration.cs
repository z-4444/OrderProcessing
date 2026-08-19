using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(item => item.Id);

        builder.Property<Guid>("OrderId").IsRequired();

        builder.Property(item => item.Sku)
            .HasConversion(sku => sku.Value, value => new Sku(value))
            .HasColumnName("Sku")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(item => item.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Quantity).IsRequired();

        builder.ComplexProperty(item => item.UnitPrice, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("UnitPrice")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName("UnitPriceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(item => item.Discount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("Discount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName("DiscountCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(item => item.LineTotal, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("LineTotal")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName("LineTotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
