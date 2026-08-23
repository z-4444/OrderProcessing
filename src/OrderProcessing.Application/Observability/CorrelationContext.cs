using OrderProcessing.Application.Abstractions;

namespace OrderProcessing.Application.Observability;

internal sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        CorrelationId = correlationId.Trim();
    }
}
