using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class DbApprovalWorkflowService : IApprovalWorkflowService
{
    private static readonly string[] DefaultApprovers = ["Ian"];

    private readonly BookingDbContext _db;

    public DbApprovalWorkflowService(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<ApprovalRequestResponse> CreateAsync(
        string bookingId,
        string changeType,
        string requestedBy,
        string? reasonCode,
        string? reasonDetail,
        CancellationToken ct)
    {
        var model = new ApprovalRequestModel
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = bookingId,
            ChangeType = changeType,
            RequestedBy = requestedBy,
            Status = "Pending",
            RequestedUtc = DateTime.UtcNow,
            ReasonCode = reasonCode,
            ReasonDetail = reasonDetail
        };

        _db.ApprovalRequests.Add(model);
        await _db.SaveChangesAsync(ct);

        return ToResponse(model);
    }

    public async Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct)
    {
        var rows = await _db.ApprovalRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.RequestedUtc)
            .ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct)
    {
        var row = await _db.ApprovalRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);

        return row is null ? null : ToResponse(row);
    }

    public async Task<ApprovalRequestResponse?> ReviewAsync(
        string requestId,
        bool approved,
        string reviewer,
        string? notes,
        CancellationToken ct)
    {
        var row = await _db.ApprovalRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (row is null)
            return null;

        if (!string.Equals(row.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return ToResponse(row);

        row.Status = approved ? "Approved" : "Rejected";
        row.Reviewer = reviewer;
        row.ReviewNotes = notes;
        row.ReviewedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToResponse(row);
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
                x => x.Id == requestId
                     && x.BookingId == bookingId
                     && x.ChangeType == changeType
                     && x.RequestedBy == requestedBy
                     && x.Status == "Approved",
                ct);
    }

    private static ApprovalRequestResponse ToResponse(ApprovalRequestModel model)
    {
        return new ApprovalRequestResponse
        {
            RequestId = model.Id,
            BookingId = model.BookingId,
            ChangeType = model.ChangeType,
            RequestedBy = model.RequestedBy,
            Status = model.Status,
            RequestedUtc = model.RequestedUtc,
            ReasonCode = model.ReasonCode,
            ReasonDetail = model.ReasonDetail,
            Reviewer = model.Reviewer,
            ReviewedUtc = model.ReviewedUtc,
            ReviewNotes = model.ReviewNotes,
            RoutedTo = DefaultApprovers
        };
    }
}
