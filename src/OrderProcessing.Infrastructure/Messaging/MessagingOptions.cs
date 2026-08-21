namespace OrderProcessing.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string ExchangeName { get; set; } = "orderprocessing.events";

    public string QueueName { get; set; } = "orderprocessing.order-events";

    public int PublisherBatchSize { get; set; } = 20;

    public int PublisherIntervalSeconds { get; set; } = 2;
}
