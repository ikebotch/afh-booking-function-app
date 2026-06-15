using System.Text.Json;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Application.Services;

public sealed class NotificationOutboxService : INotificationPublisher
{
    private readonly INotificationOutboxStore _outboxStore;
    private readonly INotificationQueuePublisher _queuePublisher;
    private readonly INotificationIdempotencyKeyGenerator _keyGenerator;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly ILogger<NotificationOutboxService> _logger;

    public NotificationOutboxService(
        INotificationOutboxStore outboxStore,
        INotificationQueuePublisher queuePublisher,
        INotificationIdempotencyKeyGenerator keyGenerator,
        INotificationRecipientResolver recipientResolver,
        ILogger<NotificationOutboxService> logger)
    {
        _outboxStore = outboxStore;
        _queuePublisher = queuePublisher;
        _keyGenerator = keyGenerator;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
        => await AcceptAsync(notification, ct);

    public async Task<NotificationOutboxAcceptResult> AcceptAsync(NotificationRequested notification, CancellationToken ct)
    {
        var route = await _recipientResolver.ResolveAsync(notification, ct);
        var accepted = new List<NotificationOutboxCreateResult>();

        foreach (var recipient in route.Recipients)
        {
            var channels = recipient.PreferredChannels ?? Array.Empty<NotificationChannel>();
            foreach (var channel in channels)
            {
                var channelNotification = CreateChannelNotification(notification, recipient, channel);
                var payloadJson = JsonSerializer.Serialize(channelNotification);
                var key = _keyGenerator.GenerateKey(channelNotification, channel, recipient);

                var outboxItem = new NotificationOutboxItem(
                    Guid.NewGuid(),
                    notification.SourceSystem,
                    notification.Type.Name,
                    key,
                    payloadJson,
                    NotificationDispatchStatus.Pending,
                    null,
                    0,
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    null);

                var result = await _outboxStore.CreateOrGetAsync(outboxItem, ct);
                accepted.Add(result);

                if (result.Created)
                {
                    var queueMessage = new NotificationQueueMessage
                    {
                        OutboxId = result.Item.Id
                    };

                    var publishResult = await _queuePublisher.PublishAsync(queueMessage, ct);

                    try
                    {
                        await _outboxStore.MarkQueuedAsync(result.Item.Id, publishResult.QueueMessageId, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Notification queue publish succeeded but marking outbox item {OutboxId} as queued failed. QueueMessageId: {QueueMessageId}",
                            result.Item.Id,
                            publishResult.QueueMessageId);
                        throw;
                    }
                }
            }
        }

        return new NotificationOutboxAcceptResult(accepted);
    }

    private static NotificationRequested CreateChannelNotification(
        NotificationRequested notification,
        NotificationRecipient recipient,
        NotificationChannel channel)
        => NotificationRecipientDataSafety.ForRecipientChannel(notification, recipient, channel);
}
