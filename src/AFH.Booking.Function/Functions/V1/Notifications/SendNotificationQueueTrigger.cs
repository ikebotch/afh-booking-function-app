using System.Text.Json;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Options;
using AFH.Notification.Application.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Functions.V1.Notifications;

public class SendNotificationQueueTrigger
{
    private readonly NotificationOutboxDispatcher _dispatcher;
    private readonly NotificationOutboxDispatchOptions _options;
    private readonly ILogger<SendNotificationQueueTrigger> _logger;

    public SendNotificationQueueTrigger(
        NotificationOutboxDispatcher dispatcher,
        IOptions<NotificationOutboxDispatchOptions> options,
        ILogger<SendNotificationQueueTrigger> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    [Function(nameof(SendNotificationQueueTrigger))]
    public async Task RunAsync(
        [QueueTrigger("%NotificationQueue:QueueName%", Connection = "NotificationQueue:ConnectionString")] string queueMessageJson,
        FunctionContext context)
    {
        var cancellationToken = context.CancellationToken;

        if (_options.IsSqlMode)
        {
            _logger.LogInformation("Queue notification dispatch skipped because DispatcherMode=Sql.");
            return;
        }

        NotificationQueueMessage? queueMessage;
        try
        {
            queueMessage = JsonSerializer.Deserialize<NotificationQueueMessage>(queueMessageJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize queue message. Message will be retried or dead-lettered.");
            throw;
        }

        if (queueMessage == null)
        {
            _logger.LogWarning("Queue message deserialized to null. Exiting.");
            return;
        }

        var outboxId = queueMessage.NotificationOutboxId;
        await _dispatcher.DispatchQueuedAsync(outboxId, cancellationToken);
    }
}
