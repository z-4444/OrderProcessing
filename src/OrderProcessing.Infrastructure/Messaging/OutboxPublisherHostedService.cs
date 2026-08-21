using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.Messaging;

internal sealed class OutboxPublisherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBusPublisher _publisher;
    private readonly MessagingOptions _options;
    private readonly ILogger<OutboxPublisherHostedService> _logger;

    public OutboxPublisherHostedService(
        IServiceScopeFactory scopeFactory,
        IEventBusPublisher publisher,
        IOptions<MessagingOptions> options,
        ILogger<OutboxPublisherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox publisher disabled (Messaging:Enabled=false).");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PublisherIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Outbox publisher loop failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderProcessingDbContext>();
        var batch = await db.OutboxMessages
            .Where(message => message.Status == OutboxMessageStatus.Pending)
            .OrderBy(message => message.OccurredAt)
            .Take(Math.Max(1, _options.PublisherBatchSize))
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                message.MarkPublished(DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message);
                _logger.LogWarning(exception, "Failed to publish outbox message {MessageId}", message.Id);
            }
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
