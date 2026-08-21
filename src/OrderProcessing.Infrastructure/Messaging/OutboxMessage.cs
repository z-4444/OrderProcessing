namespace OrderProcessing.Infrastructure.Messaging;

public enum OutboxMessageStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt,
        OutboxMessageStatus status,
        int attemptCount,
        string? correlationId,
        DateTimeOffset? processedAt,
        string? lastError)
    {
        Id = id;
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        Status = status;
        AttemptCount = attemptCount;
        CorrelationId = correlationId;
        ProcessedAt = processedAt;
        LastError = lastError;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        string eventType,
        string payloadJson,
        DateTimeOffset occurredAt,
        string? correlationId = null) =>
        new(
            Guid.NewGuid(),
            eventType,
            payloadJson,
            occurredAt,
            OutboxMessageStatus.Pending,
            0,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            null,
            null);

    public void MarkPublished(DateTimeOffset processedAt)
    {
        Status = OutboxMessageStatus.Published;
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = OutboxMessageStatus.Failed;
        AttemptCount += 1;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }

    public void IncrementAttempt() => AttemptCount += 1;
}

public sealed class ProcessedMessage
{
    private ProcessedMessage()
    {
    }

    public ProcessedMessage(Guid messageId, string eventType, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        EventType = eventType;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }
}
