using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Infrastructure.Persistence.Configurations;

internal sealed class OrderAuditEventConfiguration : IEntityTypeConfiguration<OrderAuditEvent>
{
    public void Configure(EntityTypeBuilder<OrderAuditEvent> builder)
    {
        builder.ToTable("OrderAuditEvents");
        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.EventType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(audit => audit.OccurredAt).IsRequired();
        builder.Property(audit => audit.ActorId);

        builder.Property(audit => audit.Message)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(audit => audit.DetailsJson)
            .HasMaxLength(4000);

        builder.HasIndex(audit => new { audit.OrderId, audit.OccurredAt });

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(audit => audit.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
