using Microsoft.EntityFrameworkCore;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationOutboxStore : INotificationOutboxStore
{
    private readonly DbContext _dbContext;
    private readonly ILogger<NotificationOutboxStore> _logger;

    public NotificationOutboxStore(DbContext dbContext, ILogger<NotificationOutboxStore> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<NotificationOutboxCreateResult> CreateOrGetAsync(NotificationOutboxItem item, CancellationToken ct)
    {
        var existing = await _dbContext.Set<NotificationOutboxModel>()
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
            ProcessedUtc = item.ProcessedUtc
        };

        _dbContext.Set<NotificationOutboxModel>().Add(model);

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

            var raceExisting = await _dbContext.Set<NotificationOutboxModel>()
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
        var model = await _dbContext.Set<NotificationOutboxModel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return model != null ? MapToItem(model) : null;
    }

    public async Task MarkQueuedAsync(Guid id, string queueMessageId, CancellationToken ct)
    {
        var affected = await _dbContext.Set<NotificationOutboxModel>()
            .Where(x => x.Id == id && x.Status == NotificationDispatchStatus.Pending.ToString())
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Queued.ToString())
                .SetProperty(x => x.QueueMessageId, queueMessageId)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found or is not in Pending status.");
        }
    }

    public async Task<bool> TryMarkProcessingAsync(Guid id, CancellationToken ct)
    {
        var validStatuses = new[]
        {
            NotificationDispatchStatus.Pending.ToString(),
            NotificationDispatchStatus.Queued.ToString(),
            NotificationDispatchStatus.Failed.ToString()
        };

        var affected = await _dbContext.Set<NotificationOutboxModel>()
            .Where(x => x.Id == id && validStatuses.Contains(x.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Processing.ToString())
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow), ct);

        return affected > 0;
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await _dbContext.Set<NotificationOutboxModel>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Sent.ToString())
                .SetProperty(x => x.ProcessedUtc, now)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found.");
        }
    }

    public async Task MarkFailedAsync(Guid id, string lastError, CancellationToken ct)
    {
        var affected = await _dbContext.Set<NotificationOutboxModel>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.Failed.ToString())
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found.");
        }
    }

    public async Task MarkDeadLetteredAsync(Guid id, string lastError, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await _dbContext.Set<NotificationOutboxModel>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, NotificationDispatchStatus.DeadLettered.ToString())
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.ProcessedUtc, now)
                .SetProperty(x => x.UpdatedUtc, now), ct);

        if (affected == 0)
        {
            throw new InvalidOperationException($"Notification outbox item '{id}' was not found.");
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
            model.ProcessedUtc);
    }
}
