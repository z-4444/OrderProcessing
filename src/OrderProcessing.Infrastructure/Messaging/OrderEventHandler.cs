namespace OrderProcessing.Infrastructure.Messaging;

using Microsoft.Extensions.Logging;

internal interface IOrderEventHandler
{
    Task HandleAsync(
        string eventType,
        string payloadJson,
        Guid? messageId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}

internal sealed class OrderEventLoggingHandler : IOrderEventHandler
{
    private readonly ILogger<OrderEventLoggingHandler> _logger;

    public OrderEventLoggingHandler(ILogger<OrderEventLoggingHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        string eventType,
        string payloadJson,
        Guid? messageId,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handled order event {EventType} message {MessageId} correlation {CorrelationId}: {Payload}",
            eventType,
            messageId,
            correlationId,
            payloadJson);

        return Task.CompletedTask;
    }
}
