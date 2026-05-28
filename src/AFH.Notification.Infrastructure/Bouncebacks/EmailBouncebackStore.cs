using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackStore : INotificationBouncebackStore, INotificationBounceAuditStore
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<EmailBouncebackStore> _logger;

    public EmailBouncebackStore(NotificationDbContext db, ILogger<EmailBouncebackStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordBouncebackAsync(NotificationBounceback bounceback, CancellationToken ct)
    {
        await RecordAsync(new NotificationBounceAuditRecord(
            bounceback.ProviderMessageId,
            RecipientEmail: null,
            bounceback.Status,
            bounceback.BounceReason,
            bounceback.TimestampUtc), ct);
    }

    public async Task<NotificationBounceAuditResult> RecordAsync(NotificationBounceAuditRecord record, CancellationToken ct)
    {
        _logger.LogInformation(
            "Recording bounceback for ProviderMessageId={ProviderMessageId}, Status={Status}, Reason={Reason}",
            record.ProviderMessageId,
            record.ReasonCode,
            record.ReasonDetail);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var now = DateTime.UtcNow;
            var dispatches = await _db.NotificationDispatches
                .Where(x => x.ProviderMessageId == record.ProviderMessageId)
                .ToListAsync(ct);

            if (dispatches.Count == 0)
            {
                _logger.LogWarning(
                    "Bounceback ProviderMessageId={ProviderMessageId} did not match a NotificationDispatches row; recording EmailBounceEvents only.",
                    record.ProviderMessageId);
            }

            foreach (var dispatch in dispatches)
            {
                dispatch.Status = record.ReasonCode ?? "Bounced";
                dispatch.EmailStatus = record.ReasonCode ?? "Bounced";
                dispatch.OutcomeCode = record.ReasonCode ?? "Bounced";
                dispatch.FailureDetails = record.ReasonDetail;
                dispatch.UpdatedUtc = now;
            }

            var bounceId = Guid.NewGuid().ToString("N");
            await _db.EmailBounceEvents.AddAsync(new EmailBounceEventModel
            {
                Id = bounceId,
                ProviderMessageId = record.ProviderMessageId,
                RecipientEmail = record.RecipientEmail,
                ReasonCode = record.ReasonCode,
                ReasonDetail = record.ReasonDetail,
                OccurredUtc = record.OccurredUtc,
                ReceivedUtc = now
            }, ct);

            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return new NotificationBounceAuditResult(
                bounceId,
                record.ProviderMessageId,
                record.RecipientEmail,
                record.ReasonCode,
                record.ReasonDetail,
                record.OccurredUtc,
                now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist bounceback for message {MessageId}", record.ProviderMessageId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
