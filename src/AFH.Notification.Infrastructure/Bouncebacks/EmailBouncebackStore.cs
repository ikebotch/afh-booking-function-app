using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Bouncebacks;

public sealed class EmailBouncebackStore : INotificationBouncebackStore
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
        _logger.LogInformation(
            "Recording bounceback for ProviderMessageId={ProviderMessageId}, Status={Status}, Reason={Reason}",
            bounceback.ProviderMessageId,
            bounceback.Status,
            bounceback.BounceReason);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var now = DateTime.UtcNow;
            var dispatches = await _db.NotificationDispatches
                .Where(x => x.ProviderMessageId == bounceback.ProviderMessageId)
                .ToListAsync(ct);

            if (dispatches.Count == 0)
            {
                _logger.LogWarning(
                    "Bounceback ProviderMessageId={ProviderMessageId} did not match a NotificationDispatches row; recording EmailBounceEvents only.",
                    bounceback.ProviderMessageId);
            }

            foreach (var dispatch in dispatches)
            {
                dispatch.EmailStatus = bounceback.Status;
                dispatch.OutcomeCode = bounceback.Status;
                dispatch.FailureDetails = bounceback.BounceReason;
                dispatch.UpdatedUtc = now;
            }

            await _db.EmailBounceEvents.AddAsync(new EmailBounceEventModel
            {
                Id = Guid.NewGuid().ToString("N"),
                ProviderMessageId = bounceback.ProviderMessageId,
                ReasonCode = bounceback.Status,
                ReasonDetail = bounceback.BounceReason,
                OccurredUtc = bounceback.TimestampUtc,
                ReceivedUtc = now
            }, ct);

            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist bounceback for message {MessageId}", bounceback.ProviderMessageId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
