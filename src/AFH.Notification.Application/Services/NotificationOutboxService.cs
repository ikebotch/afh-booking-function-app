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
    private readonly IContactCentreRoutingResolver _contactCentreResolver;
    private readonly ILogger<NotificationOutboxService> _logger;

    public NotificationOutboxService(
        INotificationOutboxStore outboxStore,
        INotificationQueuePublisher queuePublisher,
        INotificationIdempotencyKeyGenerator keyGenerator,
        INotificationRecipientResolver recipientResolver,
        IContactCentreRoutingResolver contactCentreResolver,
        ILogger<NotificationOutboxService> logger)
    {
        _outboxStore = outboxStore;
        _queuePublisher = queuePublisher;
        _keyGenerator = keyGenerator;
        _recipientResolver = recipientResolver;
        _contactCentreResolver = contactCentreResolver;
        _logger = logger;
    }

    public async Task PublishAsync(NotificationRequested notification, CancellationToken ct)
    {
        var route = await _recipientResolver.ResolveAsync(notification, ct);

        var payloadJson = JsonSerializer.Serialize(notification);
        var activeRecipients = route.Recipients.ToList();

        if (route.CopyContactCentre)
        {
            var ccEmail = _contactCentreResolver.GetContactCentreEmailAddress();
            if (!string.IsNullOrWhiteSpace(ccEmail) && !activeRecipients.Any(r => string.Equals(r.Email, ccEmail, StringComparison.OrdinalIgnoreCase)))
            {
                activeRecipients.Add(new NotificationRecipient("ContactCentre", "Contact Centre", ccEmail, null, null, [NotificationChannel.Email]));
            }
        }

        foreach (var recipient in activeRecipients)
        {
            var channels = recipient.PreferredChannels ?? Array.Empty<NotificationChannel>();
            foreach (var channel in channels)
            {
                var key = _keyGenerator.GenerateKey(notification, channel, recipient);

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

                if (result.Created)
                {
                    var queueMessage = new NotificationQueueMessage
                    {
                        NotificationOutboxId = result.Item.Id,
                        SourceApplication = result.Item.SourceApplication,
                        NotificationType = result.Item.NotificationType
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
    }
}
