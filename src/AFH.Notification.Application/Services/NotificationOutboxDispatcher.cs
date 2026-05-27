using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationOutboxDispatcher
{
    private readonly INotificationOutboxStore _outboxStore;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationOutboxDispatcher> _logger;

    public NotificationOutboxDispatcher(
        INotificationOutboxStore outboxStore,
        INotificationService notificationService,
        ILogger<NotificationOutboxDispatcher> logger)
    {
        _outboxStore = outboxStore;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task DispatchQueuedAsync(Guid outboxId, CancellationToken ct)
    {
        var outboxItem = await _outboxStore.GetAsync(outboxId, ct);
        if (outboxItem == null)
        {
            _logger.LogWarning("Notification outbox item not found. OutboxId={OutboxId}", outboxId);
            return;
        }

        var claimed = await _outboxStore.TryMarkProcessingAsync(
            outboxId,
            DateTime.UtcNow,
            TimeSpan.FromMinutes(5),
            ct);

        if (claimed == null)
        {
            _logger.LogInformation("Failed to claim outbox item for queue dispatch. OutboxId={OutboxId}", outboxId);
            return;
        }

        await DispatchClaimedAsync(claimed, throwOnRetryableFailure: true, ct);
    }

    private async Task DispatchClaimedAsync(
        NotificationOutboxItem outboxItem,
        bool throwOnRetryableFailure,
        CancellationToken ct)
    {
        NotificationRequested? notificationRequested;
        try
        {
            notificationRequested = JsonSerializer.Deserialize<NotificationRequested>(outboxItem.PayloadJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize NotificationRequested payload from outbox item. OutboxId={OutboxId}", outboxItem.Id);
            await _outboxStore.MarkDeadLetteredAsync(outboxItem.Id, "Invalid payload JSON.", ct);
            return;
        }

        if (notificationRequested == null)
        {
            _logger.LogError("Deserialized NotificationRequested is null. OutboxId={OutboxId}", outboxItem.Id);
            await _outboxStore.MarkDeadLetteredAsync(outboxItem.Id, "Payload JSON deserialized to null.", ct);
            return;
        }

        try
        {
            await _notificationService.PublishAsync(notificationRequested, ct);
            await _outboxStore.MarkSentAsync(outboxItem.Id, ct);

            _logger.LogInformation("Successfully dispatched notification outbox item. OutboxId={OutboxId}", outboxItem.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while dispatching notification outbox item. OutboxId={OutboxId}", outboxItem.Id);

            await _outboxStore.MarkFailedAsync(
                outboxItem.Id,
                ex.Message,
                ct);

            if (throwOnRetryableFailure)
                throw;
        }
    }
}
