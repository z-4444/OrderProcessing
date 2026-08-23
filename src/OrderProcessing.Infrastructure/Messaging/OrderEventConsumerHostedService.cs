using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderProcessing.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderProcessing.Infrastructure.Messaging;

internal sealed class OrderEventConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagingOptions _options;
    private readonly ILogger<OrderEventConsumerHostedService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderEventConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<OrderEventConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Order event consumer disabled (Messaging:Enabled=false).");
            return Task.CompletedTask;
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection("orderprocessing-consumer");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_options.QueueName, _options.ExchangeName, routingKey: "order.#");
        _channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(_options.QueueName, autoAck: false, consumer);

        stoppingToken.Register(() =>
        {
            _channel?.Close();
            _connection?.Close();
        });

        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var messageId = Guid.TryParse(args.BasicProperties.MessageId, out var id) ? id : Guid.Empty;
        var eventType = args.BasicProperties.Type ?? "unknown";
        var correlationId = args.BasicProperties.CorrelationId;
        var payload = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderProcessingDbContext>();
            var handler = scope.ServiceProvider.GetRequiredService<IOrderEventHandler>();

            if (messageId != Guid.Empty)
            {
                var alreadyProcessed = await db.ProcessedMessages.AnyAsync(message => message.MessageId == messageId);
                if (alreadyProcessed)
                {
                    _logger.LogDebug(
                        "Skipping duplicate event {EventType} message {MessageId}",
                        eventType,
                        messageId);
                    _channel?.BasicAck(args.DeliveryTag, false);
                    return;
                }
            }

            await handler.HandleAsync(
                eventType,
                payload,
                messageId == Guid.Empty ? null : messageId,
                correlationId);

            if (messageId != Guid.Empty)
            {
                db.ProcessedMessages.Add(new ProcessedMessage(messageId, eventType, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }

            _channel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed consuming event {EventType} message {MessageId}", eventType, messageId);
            _channel?.BasicNack(args.DeliveryTag, false, requeue: true);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
