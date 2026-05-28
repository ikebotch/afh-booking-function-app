using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Services;

public sealed class NotificationAdminOperationService : INotificationAdminOperationService
{
    private readonly INotificationOutboxStore _outboxStore;
    private readonly INotificationQueuePublisher _queuePublisher;

    public NotificationAdminOperationService(
        INotificationOutboxStore outboxStore,
        INotificationQueuePublisher queuePublisher)
    {
        _outboxStore = outboxStore;
        _queuePublisher = queuePublisher;
    }

    public async Task<NotificationAdminOperationResult> RequeueAsync(Guid id, CancellationToken ct)
    {
        var item = await _outboxStore.GetAsync(id, ct)
            ?? throw new NotificationRequestValidationException("Notification request was not found.");

        if (item.Status is not (NotificationDispatchStatus.Failed or NotificationDispatchStatus.DeadLettered))
            throw new NotificationRequestValidationException("Only Failed or DeadLettered notification requests can be requeued.");

        var queueResult = await _queuePublisher.PublishAsync(new NotificationQueueMessage { OutboxId = id }, ct);
        await _outboxStore.MarkRequeuedAsync(id, queueResult.QueueMessageId, ct);
        return new NotificationAdminOperationResult(id, NotificationDispatchStatus.Queued.ToString(), queueResult.QueueMessageId);
    }

    public async Task<NotificationAdminOperationResult> DeadLetterAsync(Guid id, string reason, CancellationToken ct)
    {
        var item = await _outboxStore.GetAsync(id, ct)
            ?? throw new NotificationRequestValidationException("Notification request was not found.");

        if (item.Status is not (NotificationDispatchStatus.Processing or NotificationDispatchStatus.Failed))
            throw new NotificationRequestValidationException("Only Processing or Failed notification requests can be dead-lettered.");

        await _outboxStore.MarkDeadLetteredAsync(id, string.IsNullOrWhiteSpace(reason) ? "Admin dead-lettered" : reason.Trim(), ct);
        return new NotificationAdminOperationResult(id, NotificationDispatchStatus.DeadLettered.ToString());
    }

    public async Task<NotificationAdminOperationResult> MarkFailedAsync(Guid id, string reason, CancellationToken ct)
    {
        var item = await _outboxStore.GetAsync(id, ct)
            ?? throw new NotificationRequestValidationException("Notification request was not found.");

        if (item.Status == NotificationDispatchStatus.Sent)
            throw new NotificationRequestValidationException("Sent notification requests cannot be marked failed.");

        await _outboxStore.MarkFailedFromAdminAsync(id, string.IsNullOrWhiteSpace(reason) ? "Admin marked failed" : reason.Trim(), ct);
        return new NotificationAdminOperationResult(id, NotificationDispatchStatus.Failed.ToString());
    }
}
