using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderProcessing.Infrastructure.Messaging;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.EventType).HasMaxLength(128).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(message => message.CorrelationId).HasMaxLength(128);
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.AttemptCount).IsRequired();

        builder.HasIndex(message => new { message.Status, message.OccurredAt });
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");
        builder.HasKey(message => message.MessageId);
        builder.Property(message => message.EventType).HasMaxLength(128).IsRequired();
        builder.Property(message => message.ProcessedAt).IsRequired();
    }
}
