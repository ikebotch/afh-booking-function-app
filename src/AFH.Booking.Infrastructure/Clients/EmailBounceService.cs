using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class EmailBounceService : IEmailBounceService
{
    private readonly NotificationDbContext _db;

    public EmailBounceService(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<EmailBounceEventResponse> RecordBounceAsync(EmailBounceWebhookRequest request, CancellationToken ct)
    {
        var occurredUtc = request.OccurredUtc ?? DateTime.UtcNow;

        var model = new EmailBounceEventModel
        {
            Id = Guid.NewGuid().ToString("N"),
            ProviderMessageId = request.ProviderMessageId,
            RecipientEmail = request.RecipientEmail,
            ReasonCode = request.ReasonCode,
            ReasonDetail = request.ReasonDetail,
            OccurredUtc = occurredUtc,
            ReceivedUtc = DateTime.UtcNow
        };

        _db.EmailBounceEvents.Add(model);

        if (!string.IsNullOrWhiteSpace(request.ProviderMessageId))
        {
            var dispatch = await _db.NotificationDispatches
                .SingleOrDefaultAsync(x => x.ProviderMessageId == request.ProviderMessageId, ct);

            if (dispatch is not null)
            {
                dispatch.EmailStatus = "Bounced";
                dispatch.UpdatedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new EmailBounceEventResponse
        {
            BounceId = model.Id,
            ProviderMessageId = model.ProviderMessageId,
            RecipientEmail = model.RecipientEmail,
            ReasonCode = model.ReasonCode,
            ReasonDetail = model.ReasonDetail,
            OccurredUtc = model.OccurredUtc,
            ReceivedUtc = model.ReceivedUtc
        };
    }
}
