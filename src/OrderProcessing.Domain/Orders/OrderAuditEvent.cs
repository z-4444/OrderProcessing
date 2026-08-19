using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Orders;

public sealed class OrderAuditEvent
{
    private OrderAuditEvent()
    {
    }

    private OrderAuditEvent(
        Guid id,
        Guid orderId,
        OrderAuditEventType eventType,
        DateTimeOffset occurredAt,
        Guid? actorId,
        string message,
        string? detailsJson)
    {
        Id = id;
        OrderId = orderId;
        EventType = eventType;
        OccurredAt = occurredAt;
        ActorId = actorId;
        Message = message;
        DetailsJson = detailsJson;
    }

    public Guid Id { get; }

    public Guid OrderId { get; }

    public OrderAuditEventType EventType { get; }

    public DateTimeOffset OccurredAt { get; }

    public Guid? ActorId { get; }

    public string Message { get; } = string.Empty;

    public string? DetailsJson { get; }

    public static OrderAuditEvent Create(
        Guid orderId,
        OrderAuditEventType eventType,
        DateTimeOffset occurredAt,
        string message,
        Guid? actorId = null,
        string? detailsJson = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order id is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Audit message is required.");
        }

        return new OrderAuditEvent(
            Guid.NewGuid(),
            orderId,
            eventType,
            occurredAt,
            actorId,
            message.Trim(),
            string.IsNullOrWhiteSpace(detailsJson) ? null : detailsJson);
    }
}
