using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Inventory;
using OrderProcessing.Domain.Products;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems", table =>
        {
            table.HasCheckConstraint("CK_InventoryItems_QuantityOnHand", "[QuantityOnHand] >= 0");
            table.HasCheckConstraint("CK_InventoryItems_QuantityReserved", "[QuantityReserved] >= 0");
            table.HasCheckConstraint(
                "CK_InventoryItems_ReservedNotAboveOnHand",
                "[QuantityReserved] <= [QuantityOnHand]");
        });

        builder.HasKey(item => item.ProductId);

        builder.Property(item => item.QuantityOnHand).IsRequired();
        builder.Property(item => item.QuantityReserved).IsRequired();
        builder.Ignore(item => item.AvailableQuantity);

        builder.HasOne<Product>()
            .WithOne()
            .HasForeignKey<InventoryItem>(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // RowVersion detects lost updates. It does not replace the atomic
        // conditional UPDATE that Milestone 4 will use to prevent overselling.
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();
    }
}
