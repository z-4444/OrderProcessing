using OrderProcessing.Infrastructure.Messaging;

namespace OrderProcessing.UnitTests.Messaging;

public sealed class OutboxMessageTests
{
    [Fact]
    public void RecordPublishFailure_RetriesUntilMaxThenMarksFailed()
    {
        var message = OutboxMessage.Create("order.submitted", "{}", DateTimeOffset.UtcNow);

        message.RecordPublishFailure("broker down", maxAttempts: 3);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.AttemptCount);

        message.RecordPublishFailure("broker down", maxAttempts: 3);
        message.RecordPublishFailure("broker down", maxAttempts: 3);
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(3, message.AttemptCount);
    }
}
