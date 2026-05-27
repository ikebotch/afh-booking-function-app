using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NoOpNotificationQueuePublisher : INotificationQueuePublisher
{
    private readonly ILogger<NoOpNotificationQueuePublisher> _logger;

    public NoOpNotificationQueuePublisher(ILogger<NoOpNotificationQueuePublisher> logger)
    {
        _logger = logger;
    }

    public Task<NotificationQueuePublishResult> PublishAsync(NotificationQueueMessage message, CancellationToken ct)
    {
        _logger.LogInformation("NoOp queue publisher invoked for Outbox ID {OutboxId}", message.NotificationOutboxId);
        return Task.FromResult(new NotificationQueuePublishResult($"noop-{message.NotificationOutboxId:N}"));
    }
}
