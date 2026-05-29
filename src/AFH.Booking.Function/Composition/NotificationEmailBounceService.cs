using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;

namespace AFH.Booking.Function.Composition;

public sealed class NotificationEmailBounceService : IEmailBounceService
{
    private readonly INotificationBounceAuditStore _bounceAuditStore;

    public NotificationEmailBounceService(INotificationBounceAuditStore bounceAuditStore)
    {
        _bounceAuditStore = bounceAuditStore;
    }

    public async Task<EmailBounceEventResponse> RecordBounceAsync(EmailBounceWebhookRequest request, CancellationToken ct)
    {
        var occurredUtc = request.OccurredUtc ?? DateTime.UtcNow;

        var result = await _bounceAuditStore.RecordAsync(new NotificationBounceAuditRecord(
            request.ProviderMessageId,
            request.RecipientEmail,
            request.ReasonCode,
            request.ReasonDetail,
            occurredUtc), ct);

        return new EmailBounceEventResponse
        {
            BounceId = result.BounceId,
            ProviderMessageId = result.ProviderMessageId,
            RecipientEmail = result.RecipientEmail,
            ReasonCode = result.ReasonCode,
            ReasonDetail = result.ReasonDetail,
            OccurredUtc = result.OccurredUtc,
            ReceivedUtc = result.ReceivedUtc
        };
    }
}
