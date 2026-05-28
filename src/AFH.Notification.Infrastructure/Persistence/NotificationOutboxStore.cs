using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationOutboxStore : INotificationOutboxStore
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<NotificationOutboxStore> _logger;

    public NotificationOutboxStore(NotificationDbContext dbContext, ILogger<NotificationOutboxStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<NotificationOutboxCreateResult> CreateOrGetAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        var existing = await _dbContext.NotificationOutbox
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == item.IdempotencyKey, ct);

        if (existing != null)
        {
            _logger.LogInformation("Duplicate IdempotencyKey '{IdempotencyKey}' detected. Returning existing outbox item.", item.IdempotencyKey);
            return new NotificationOutboxCreateResult(MapToItem(existing), false);
        }

        var model = new NotificationOutboxModel
        {
            Id = item.Id,
            SourceApplication = item.SourceApplication,
            NotificationType = item.NotificationType,
            IdempotencyKey = item.IdempotencyKey,
            PayloadJson = item.PayloadJson,
            Status = item.Status.ToString(),
            QueueMessageId = item.QueueMessageId,
            AttemptCount = item.AttemptCount,
            LastError = item.LastError,
            CreatedUtc = item.CreatedUtc,
            UpdatedUtc = item.UpdatedUtc,
            ProcessedUtc = item.ProcessedUtc,
            NextAttemptUtc = item.NextAttemptUtc,
            LockedUntilUtc = item.LockedUntilUtc
        };

        _dbContext.NotificationOutbox.Add(model);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return new NotificationOutboxCreateResult(item, true);
        }
        catch (DbUpdateException ex)
        {
            // Concurrency/Duplicate Key check
            _logger.LogInformation(ex, "DbUpdateException on IdempotencyKey '{IdempotencyKey}'. Assuming race condition duplicate.", item.IdempotencyKey);

            // Clear change tracker so we don't hold the failed insert
            _dbContext.ChangeTracker.Clear();

            var raceExisting = await _dbContext.NotificationOutbox
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == item.IdempotencyKey, ct);

            if (raceExisting != null)
            {
                return new NotificationOutboxCreateResult(MapToItem(raceExisting), false);
            }

            throw; // Real DbUpdateException unrelated to unique constraint
        }
    }

    public async Task<NotificationOutboxItem?> GetAsync(Guid id, CancellationToken ct)
    {
        var model = await _dbContext.NotificationOutbox
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return model != null ? MapToItem(model) : null;
    }

    public async Task MarkQueuedAsync(Guid id, string queueMessageId, CancellationToken ct)
    {
        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && x.Status == NotificationDispatchStatus.Pending.ToString())
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Queued.ToString())
                .SetProperty(x => x.QueueMessageId, queueMessageId)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not in Pending status.");
        }
    }

    public async Task<NotificationOutboxItem?> TryMarkProcessingAsync(
        Guid id,
        DateTime utcNow,
        TimeSpan processingLock,
        CancellationToken ct)
    {
        var validStatuses = new[]
        {
            NotificationDispatchStatus.Pending.ToString(),
            NotificationDispatchStatus.Queued.ToString(),
            NotificationDispatchStatus.Failed.ToString()
        };

        var lockedUntilUtc = utcNow.Add(processingLock);
        var affected = await _dbContext.NotificationOutbox
            .Where(x =>
                x.Id == id &&
                (validStatuses.Contains(x.Status) ||
                 (x.Status == NotificationDispatchStatus.Processing.ToString() && x.LockedUntilUtc != null && x.LockedUntilUtc <= utcNow)))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Processing.ToString())
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.NextAttemptUtc, (DateTime?)null)
                .SetProperty(x => x.LockedUntilUtc, lockedUntilUtc)
                .SetProperty(x => x.ProcessedUtc, (DateTime?)null)
                .SetProperty(x => x.UpdatedUtc, utcNow), ct);

        if (affected == 0)
            return null;

        var model = await _dbContext.NotificationOutbox
            .AsNoTracking()
            .FirstAsync(x => x.Id == id, ct);

        return MapToItem(model);
    }

    public async Task<bool> TryMarkProcessingAsync(Guid id, CancellationToken ct)
        => await TryMarkProcessingAsync(id, DateTime.UtcNow, TimeSpan.FromMinutes(5), ct) != null;

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && x.Status == NotificationDispatchStatus.Processing.ToString())
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Sent.ToString())
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.NextAttemptUtc, (DateTime?)null)
                .SetProperty(x => x.LockedUntilUtc, (DateTime?)null)
                .SetProperty(x => x.ProcessedUtc, now)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not in Processing status.");
        }
    }

    public async Task MarkFailedAsync(Guid id, string lastError, DateTime nextAttemptUtc, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && x.Status == NotificationDispatchStatus.Processing.ToString())
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Failed.ToString())
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.NextAttemptUtc, nextAttemptUtc)
                .SetProperty(x => x.LockedUntilUtc, (DateTime?)null)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not in Processing status.");
        }
    }

    public Task MarkFailedAsync(Guid id, string lastError, CancellationToken ct)
        => MarkFailedAsync(id, lastError, DateTime.UtcNow, ct);

    public async Task MarkFailedFromAdminAsync(Guid id, string lastError, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && x.Status != NotificationDispatchStatus.Sent.ToString())
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Failed.ToString())
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.NextAttemptUtc, (DateTime?)null)
                .SetProperty(x => x.LockedUntilUtc, (DateTime?)null)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or cannot be marked failed.");
        }
    }

    public async Task MarkDeadLetteredAsync(Guid id, string lastError, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var validStatuses = new[]
        {
            NotificationDispatchStatus.Processing.ToString(),
            NotificationDispatchStatus.Failed.ToString()
        };

        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && validStatuses.Contains(x.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.DeadLettered.ToString())
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.NextAttemptUtc, (DateTime?)null)
                .SetProperty(x => x.LockedUntilUtc, (DateTime?)null)
                .SetProperty(x => x.ProcessedUtc, now)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not in Processing/Failed status.");
        }
    }

    public async Task MarkRequeuedAsync(Guid id, string queueMessageId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var validStatuses = new[]
        {
            NotificationDispatchStatus.Failed.ToString(),
            NotificationDispatchStatus.DeadLettered.ToString()
        };

        var affected = await _dbContext.NotificationOutbox
            .Where(x => x.Id == id && validStatuses.Contains(x.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Queued.ToString())
                .SetProperty(x => x.QueueMessageId, queueMessageId)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.NextAttemptUtc, (DateTime?)null)
                .SetProperty(x => x.LockedUntilUtc, (DateTime?)null)
                .SetProperty(x => x.ProcessedUtc, (DateTime?)null)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not Failed/DeadLettered.");
        }
    }

    private static NotificationOutboxItem MapToItem(NotificationOutboxModel model)
    {
        if (!Enum.TryParse<NotificationDispatchStatus>(model.Status, out var status))
        {
            throw new InvalidOperationException($"Unknown database status '{model.Status}' for outbox item {model.Id}");
        }

        return new NotificationOutboxItem(
            model.Id,
            model.SourceApplication,
            model.NotificationType,
            model.IdempotencyKey,
            model.PayloadJson,
            status,
            model.QueueMessageId,
            model.AttemptCount,
            model.LastError,
            model.CreatedUtc,
            model.UpdatedUtc,
            model.ProcessedUtc,
            model.NextAttemptUtc,
            model.LockedUntilUtc);
    }
}
