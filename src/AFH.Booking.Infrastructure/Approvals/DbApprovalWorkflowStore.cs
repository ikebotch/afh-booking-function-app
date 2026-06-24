using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Mapping;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class DbApprovalWorkflowStore : IApprovalWorkflowStore
{
    private readonly BookingDbContext _db;

    public DbApprovalWorkflowStore(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<ApprovalBookingSnapshot> LoadBookingAsync(
        string bookingId,
        CancellationToken ct)
    {
        var hold = await _db.Holds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new InvalidOperationException($"Booking '{bookingId}' was not found.");
        var slot = await _db.BookingSlots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == hold.SlotId, ct)
            ?? throw new InvalidOperationException($"Slot '{hold.SlotId}' was not found.");
        var tx = await _db.BookingTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == slot.TransactionId, ct)
            ?? throw new InvalidOperationException($"Transaction '{slot.TransactionId}' was not found.");

        return new ApprovalBookingSnapshot(
            BookingHoldMapping.ToDomain(hold),
            BookingSlotMapping.ToDomain(slot),
            tx.ToDomain(includeSlots: false));
    }

    public Task AddRequestAsync(
        ApprovalWorkflowRecord request,
        ApprovalHistoryRecord history,
        CancellationToken ct)
    {
        _db.ApprovalRequests.Add(ToModel(request));
        _db.ApprovalHistory.Add(ToModel(history));
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ApprovalWorkflowRecord>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<IReadOnlyList<ApprovalWorkflowRecord>> ListAsync(
        ListApprovalWorkflowRequestsQuery query,
        CancellationToken ct)
    {
        var rows = _db.ApprovalRequests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.RequesterId))
        {
            var requesterId = query.RequesterId.Trim();
            rows = rows.Where(x => x.RequesterId == requesterId);
        }

        if (query.BookingIds.Count > 0)
        {
            var bookingIds = Normalize(query.BookingIds);
            rows = rows.Where(x => bookingIds.Contains(x.BookingId));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = Normalize(query.Statuses);
            rows = rows.Where(x => statuses.Contains(x.Status));
        }

        if (query.ChangeTypes.Count > 0)
        {
            var changeTypes = Normalize(query.ChangeTypes);
            rows = rows.Where(x => changeTypes.Contains(x.ChangeType));
        }

        var results = await rows
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return results.Select(ToRecord).ToList();
    }

    private static string[] Normalize(IReadOnlyList<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<ApprovalWorkflowRecord?> GetAsync(string requestId, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task<ApprovalWorkflowRecord?> GetForUpdateAsync(string requestId, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        return row is null ? null : ToRecord(row);
    }

    public async Task UpdateAsync(ApprovalWorkflowRecord request, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.SingleAsync(x => x.Id == request.Id, ct);
        Apply(request, row);
    }

    public Task AddHistoryAsync(ApprovalHistoryRecord history, CancellationToken ct)
    {
        _db.ApprovalHistory.Add(ToModel(history));
        return Task.CompletedTask;
    }

    public async Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == requestId &&
                     x.BookingId == bookingId &&
                     x.ChangeType == changeType &&
                     x.RequestedBy == requestedBy &&
                     x.Status == "Approved",
                ct);
    }

    private static ApprovalWorkflowRecord ToRecord(ApprovalRequestModel model)
    {
        return new ApprovalWorkflowRecord
        {
            Id = model.Id,
            BookingId = model.BookingId,
            TransactionId = model.TransactionId,
            ChangeType = model.ChangeType,
            RequestedBy = model.RequestedBy,
            RequesterId = model.RequesterId,
            Status = model.Status,
            RequestedUtc = model.RequestedUtc,
            ReasonCode = model.ReasonCode,
            ReasonDetail = model.ReasonDetail,
            RequestedPayloadJson = model.RequestedPayloadJson,
            ApproverTargetType = model.ApproverTargetType,
            ApproverTargetValue = model.ApproverTargetValue,
            ApproverTargetDisplayName = model.ApproverTargetDisplayName,
            Reviewer = model.Reviewer,
            ReviewedUtc = model.ReviewedUtc,
            ReviewNotes = model.ReviewNotes,
            ExecutedUtc = model.ExecutedUtc,
            ExecutionError = model.ExecutionError
        };
    }

    private static ApprovalRequestModel ToModel(ApprovalWorkflowRecord record)
    {
        var model = new ApprovalRequestModel();
        Apply(record, model);
        return model;
    }

    private static void Apply(ApprovalWorkflowRecord source, ApprovalRequestModel target)
    {
        target.Id = source.Id;
        target.BookingId = source.BookingId;
        target.TransactionId = source.TransactionId;
        target.ChangeType = source.ChangeType;
        target.RequestedBy = source.RequestedBy;
        target.RequesterId = source.RequesterId;
        target.Status = source.Status;
        target.RequestedUtc = source.RequestedUtc;
        target.ReasonCode = source.ReasonCode;
        target.ReasonDetail = source.ReasonDetail;
        target.RequestedPayloadJson = source.RequestedPayloadJson;
        target.ApproverTargetType = source.ApproverTargetType ?? string.Empty;
        target.ApproverTargetValue = source.ApproverTargetValue ?? string.Empty;
        target.ApproverTargetDisplayName = source.ApproverTargetDisplayName ?? string.Empty;
        target.Reviewer = source.Reviewer;
        target.ReviewedUtc = source.ReviewedUtc;
        target.ReviewNotes = source.ReviewNotes;
        target.ExecutedUtc = source.ExecutedUtc;
        target.ExecutionError = source.ExecutionError;
    }

    private static ApprovalHistoryModel ToModel(ApprovalHistoryRecord record)
    {
        return new ApprovalHistoryModel
        {
            Id = record.Id,
            ApprovalRequestId = record.ApprovalRequestId,
            EventType = record.EventType,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            Outcome = record.Outcome,
            Comments = record.Comments,
            OccurredUtc = record.OccurredUtc
        };
    }
}
