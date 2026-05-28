using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationStatusService : INotificationStatusService
{
    private readonly NotificationDbContext _db;

    public NotificationStatusService(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationRequestStatus?> GetRequestAsync(Guid id, CancellationToken ct)
    {
        var request = await _db.NotificationOutbox.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null)
            return null;

        var dispatches = await _db.NotificationDispatches
            .AsNoTracking()
            .Where(x => x.NotificationOutboxId == id)
            .GroupJoin(
                _db.NotificationMessageLogs.AsNoTracking(),
                dispatch => dispatch.DispatchUid,
                log => log.NotificationDispatchId,
                (dispatch, logs) => new { dispatch, log = logs.FirstOrDefault() })
            .Select(x => new NotificationDispatchSummary(
                x.dispatch.Id,
                x.dispatch.DispatchUid,
                x.dispatch.NotificationOutboxId,
                x.dispatch.SourceApplication,
                x.dispatch.SourceReferenceType,
                x.dispatch.SourceReferenceId,
                x.dispatch.NotificationType,
                x.dispatch.CorrelationId,
                x.dispatch.RecipientType,
                x.dispatch.RecipientEmail,
                x.dispatch.RecipientMobile,
                x.dispatch.Channel,
                x.dispatch.ProviderName,
                x.dispatch.ProviderMessageId,
                x.dispatch.TemplateKey,
                x.dispatch.TemplateVersion,
                x.dispatch.Status,
                x.dispatch.FailureDetails,
                x.log == null ? null : x.log.Id,
                x.dispatch.CreatedUtc,
                x.dispatch.UpdatedUtc,
                x.dispatch.CompletedUtc))
            .ToArrayAsync(ct);

        return new NotificationRequestStatus(
            request.Id,
            request.SourceApplication,
            request.NotificationType,
            request.IdempotencyKey,
            ParseStatus(request.Status),
            request.AttemptCount,
            request.LastError,
            request.CreatedUtc,
            request.UpdatedUtc,
            dispatches);
    }

    public async Task<IReadOnlyList<NotificationRequestSummary>> QueryRequestsAsync(NotificationRequestQuery query, CancellationToken ct)
    {
        var rows = _db.NotificationOutbox.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SourceApplication))
            rows = rows.Where(x => x.SourceApplication == query.SourceApplication.Trim());
        if (!string.IsNullOrWhiteSpace(query.NotificationType))
            rows = rows.Where(x => x.NotificationType == query.NotificationType.Trim());
        if (query.Status is { } status)
            rows = rows.Where(x => x.Status == status.ToString());
        if (query.FromUtc is { } fromUtc)
            rows = rows.Where(x => x.CreatedUtc >= fromUtc);
        if (query.ToUtc is { } toUtc)
            rows = rows.Where(x => x.CreatedUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(query.SourceReferenceId) || !string.IsNullOrWhiteSpace(query.SourceReferenceType))
        {
            var outboxIds = _db.NotificationDispatches
                .AsNoTracking()
                .Where(x =>
                    x.NotificationOutboxId != null &&
                    (string.IsNullOrWhiteSpace(query.SourceReferenceId) || x.SourceReferenceId == query.SourceReferenceId.Trim()) &&
                    (string.IsNullOrWhiteSpace(query.SourceReferenceType) || x.SourceReferenceType == query.SourceReferenceType.Trim()))
                .Select(x => x.NotificationOutboxId!.Value);

            rows = rows.Where(x => outboxIds.Contains(x.Id));
        }

        return await rows
            .OrderByDescending(x => x.CreatedUtc)
            .Take(100)
            .Select(x => new NotificationRequestSummary(
                x.Id,
                x.SourceApplication,
                x.NotificationType,
                ParseStatus(x.Status),
                x.AttemptCount,
                x.LastError,
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToArrayAsync(ct);
    }

    public async Task<NotificationDispatchSummary?> GetDispatchAsync(string id, CancellationToken ct)
    {
        var row = await _db.NotificationDispatches
            .AsNoTracking()
            .GroupJoin(
                _db.NotificationMessageLogs.AsNoTracking(),
                dispatch => dispatch.DispatchUid,
                log => log.NotificationDispatchId,
                (dispatch, logs) => new { dispatch, log = logs.FirstOrDefault() })
            .SingleOrDefaultAsync(x => x.dispatch.Id == id, ct);

        return row is null
            ? null
            : new NotificationDispatchSummary(
                row.dispatch.Id,
                row.dispatch.DispatchUid,
                row.dispatch.NotificationOutboxId,
                row.dispatch.SourceApplication,
                row.dispatch.SourceReferenceType,
                row.dispatch.SourceReferenceId,
                row.dispatch.NotificationType,
                row.dispatch.CorrelationId,
                row.dispatch.RecipientType,
                row.dispatch.RecipientEmail,
                row.dispatch.RecipientMobile,
                row.dispatch.Channel,
                row.dispatch.ProviderName,
                row.dispatch.ProviderMessageId,
                row.dispatch.TemplateKey,
                row.dispatch.TemplateVersion,
                row.dispatch.Status,
                row.dispatch.FailureDetails,
                row.log?.Id,
                row.dispatch.CreatedUtc,
                row.dispatch.UpdatedUtc,
                row.dispatch.CompletedUtc);
    }

    public async Task<NotificationMessageLogDetail?> GetMessageLogAsync(Guid id, CancellationToken ct)
    {
        var row = await _db.NotificationMessageLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null
            ? null
            : new NotificationMessageLogDetail(
                row.Id,
                row.NotificationDispatchId,
                row.NotificationOutboxId,
                row.SourceApplication,
                row.NotificationType,
                row.CorrelationId,
                row.RecipientType,
                row.RecipientEmail,
                row.RecipientMobile,
                row.Channel,
                row.TemplateKey,
                row.TemplateVersion,
                row.Subject,
                row.Body,
                row.ContentType,
                row.RenderDataJson,
                row.BodyHash,
                row.CreatedUtc);
    }

    private static NotificationDispatchStatus ParseStatus(string value)
        => Enum.TryParse<NotificationDispatchStatus>(value, out var status)
            ? status
            : NotificationDispatchStatus.Failed;
}
