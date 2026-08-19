using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Sku)
            .HasConversion(sku => sku.Value, value => new Sku(value))
            .HasColumnName("Sku")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000);

        builder.ComplexProperty(product => product.UnitPrice, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("UnitPrice")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(value => value.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(product => product.IsActive).IsRequired();
        builder.Property(product => product.CreatedAt).IsRequired();
        builder.Property(product => product.UpdatedAt).IsRequired();

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }
}
