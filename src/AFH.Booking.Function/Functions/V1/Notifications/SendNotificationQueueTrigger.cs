using System.Text.Json;
using AFH.Notification.Application.Models;
using AFH.Notification.Application.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Functions.V1.Notifications;

public class SendNotificationQueueTrigger
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly NotificationOutboxDispatcher _dispatcher;
    private readonly ILogger<SendNotificationQueueTrigger> _logger;

    public SendNotificationQueueTrigger(
        NotificationOutboxDispatcher dispatcher,
        ILogger<SendNotificationQueueTrigger> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    [Function(nameof(SendNotificationQueueTrigger))]
    public async Task RunAsync(
        [QueueTrigger("%NotificationQueue:QueueName%", Connection = "NotificationQueue:ConnectionString")] string queueMessageJson,
        FunctionContext context)
    {
        var cancellationToken = context.CancellationToken;

        NotificationQueueMessage? queueMessage;
        try
        {
            queueMessage = JsonSerializer.Deserialize<NotificationQueueMessage>(queueMessageJson, SerializerOptions);
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

        await _dispatcher.DispatchQueuedAsync(queueMessage.OutboxId, cancellationToken);
    }
}
