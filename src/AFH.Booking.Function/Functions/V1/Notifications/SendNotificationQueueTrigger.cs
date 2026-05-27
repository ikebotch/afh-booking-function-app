using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Function.Functions.V1.Notifications;

public class SendNotificationQueueTrigger
{
    private readonly INotificationOutboxStore _outboxStore;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SendNotificationQueueTrigger> _logger;

    public SendNotificationQueueTrigger(
        INotificationOutboxStore outboxStore,
        INotificationService notificationService,
        ILogger<SendNotificationQueueTrigger> logger)
    {
        _outboxStore = outboxStore;
        _notificationService = notificationService;
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

        var outboxItem = await _outboxStore.GetAsync(outboxId, cancellationToken);
        if (outboxItem == null)
        {
            _logger.LogWarning("Notification outbox item not found. OutboxId={OutboxId}", outboxId);
            return;
        }

        var claimed = await _outboxStore.TryMarkProcessingAsync(outboxId, cancellationToken);
        if (!claimed)
        {
            _logger.LogInformation("Failed to claim outbox item (already processing, sent, or dead-lettered). OutboxId={OutboxId}", outboxId);
            return;
        }

        NotificationRequested? notificationRequested;
        try
        {
            notificationRequested = JsonSerializer.Deserialize<NotificationRequested>(outboxItem.PayloadJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize NotificationRequested payload from outbox item. OutboxId={OutboxId}", outboxId);
            await _outboxStore.MarkDeadLetteredAsync(outboxId, "Invalid payload JSON.", cancellationToken);
            return;
        }

        if (notificationRequested == null)
        {
            _logger.LogError("Deserialized NotificationRequested is null. OutboxId={OutboxId}", outboxId);
            await _outboxStore.MarkDeadLetteredAsync(outboxId, "Payload JSON deserialized to null.", cancellationToken);
            return;
        }

        try
        {
            await _notificationService.PublishAsync(notificationRequested, cancellationToken);
            await _outboxStore.MarkSentAsync(outboxId, cancellationToken);
            
            _logger.LogInformation("Successfully sent notification and marked outbox item as Sent. OutboxId={OutboxId}", outboxId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing NotificationService for OutboxId={OutboxId}", outboxId);
            await _outboxStore.MarkFailedAsync(outboxId, ex.Message, cancellationToken);
            throw; // Rethrow so Azure Functions triggers its built-in retry/poison-queue handling
        }
    }
}
