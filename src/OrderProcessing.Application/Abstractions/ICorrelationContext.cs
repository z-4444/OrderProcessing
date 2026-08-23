namespace OrderProcessing.Application.Abstractions;

/// <summary>
/// Request-scoped correlation identifier propagated through logs and outbox events.
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }

    void SetCorrelationId(string correlationId);
}
