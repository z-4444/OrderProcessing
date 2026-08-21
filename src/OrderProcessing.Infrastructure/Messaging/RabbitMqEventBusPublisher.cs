using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderProcessing.Infrastructure.Messaging;

public interface IEventBusPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

internal sealed class RabbitMqEventBusPublisher : IEventBusPublisher, IDisposable
{
    private readonly MessagingOptions _options;
    private readonly ILogger<RabbitMqEventBusPublisher> _logger;
    private readonly object _sync = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqEventBusPublisher(IOptions<MessagingOptions> options, ILogger<RabbitMqEventBusPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        EnsureChannel();
        var body = Encoding.UTF8.GetBytes(message.PayloadJson);
        var properties = _channel!.CreateBasicProperties();
        properties.ContentType = "application/json";
        properties.DeliveryMode = 2;
        properties.MessageId = message.Id.ToString();
        properties.Type = message.EventType;
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            properties.CorrelationId = message.CorrelationId;
        }

        _channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: message.EventType,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Published outbox message {MessageId} ({EventType})", message.Id, message.EventType);
        return Task.CompletedTask;
    }

    private void EnsureChannel()
    {
        lock (_sync)
        {
            if (_channel is { IsOpen: true })
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection("orderprocessing-publisher");
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(_options.QueueName, _options.ExchangeName, routingKey: "order.#");
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

internal sealed class NoOpEventBusPublisher : IEventBusPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
