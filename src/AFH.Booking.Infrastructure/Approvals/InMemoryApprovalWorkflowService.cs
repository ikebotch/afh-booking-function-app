using System.Collections.Concurrent;
using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Contracts.V1.Responses;

namespace AFH.Booking.Infrastructure.Approvals;

public sealed class InMemoryApprovalWorkflowService : IApprovalWorkflowService
{
    private sealed class ApprovalRecord
    {
        public string RequestId { get; init; } = default!;
        public string BookingId { get; init; } = default!;
        public string ChangeType { get; init; } = default!;
        public string RequestedBy { get; init; } = default!;
        public string Status { get; set; } = "Pending";
        public DateTime RequestedUtc { get; init; }
        public string? ReasonCode { get; init; }
        public string? ReasonDetail { get; init; }
        public string? Reviewer { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string? ReviewNotes { get; set; }
    }

    private static readonly string[] DefaultApprovers = ["Booking Approvers"];

    private readonly ConcurrentDictionary<string, ApprovalRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public Task<ApprovalRequestResponse> CreateAsync(CreateApprovalWorkflowRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid().ToString("N");

        var record = new ApprovalRecord
        {
            RequestId = id,
            BookingId = request.BookingId,
            ChangeType = request.ChangeType,
            RequestedBy = request.RequestedBy,
            RequestedUtc = now,
            ReasonCode = request.ReasonCode,
            ReasonDetail = request.ReasonDetail
        };

        _records[id] = record;

        return Task.FromResult(ToResponse(record));
    }

    public Task<IReadOnlyList<ApprovalRequestResponse>> ListPendingAsync(CancellationToken ct)
    {
        var pending = _records.Values
            .Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RequestedUtc)
            .Select(ToResponse)
            .ToList();

        return Task.FromResult<IReadOnlyList<ApprovalRequestResponse>>(pending);
    }

    public Task<ApprovalRequestResponse?> GetAsync(string requestId, CancellationToken ct)
    {
        if (!_records.TryGetValue(requestId, out var record))
            return Task.FromResult<ApprovalRequestResponse?>(null);

        return Task.FromResult<ApprovalRequestResponse?>(ToResponse(record));
    }

    public Task<ApprovalRequestResponse?> ReviewAsync(ReviewApprovalWorkflowRequest request, CancellationToken ct)
    {
        if (!_records.TryGetValue(request.RequestId, out var record))
            return Task.FromResult<ApprovalRequestResponse?>(null);

        if (!string.Equals(record.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<ApprovalRequestResponse?>(ToResponse(record));

        record.Status = request.Approved ? "Approved" : "Rejected";
        record.Reviewer = request.Reviewer;
        record.ReviewNotes = request.Notes;
        record.ReviewedUtc = DateTime.UtcNow;

        return Task.FromResult<ApprovalRequestResponse?>(ToResponse(record));
    }

    public Task<bool> IsApprovedAsync(
        string requestId,
        string bookingId,
        string changeType,
        string requestedBy,
        CancellationToken ct)
    {
        if (!_records.TryGetValue(requestId, out var record))
            return Task.FromResult(false);

        var approved =
            string.Equals(record.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.BookingId, bookingId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.ChangeType, changeType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.RequestedBy, requestedBy, StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(approved);
    }

    private static ApprovalRequestResponse ToResponse(ApprovalRecord record)
    {
        return new ApprovalRequestResponse
        {
            RequestId = record.RequestId,
            BookingId = record.BookingId,
            TransactionId = "in-memory",
            ChangeType = record.ChangeType,
            RequestedBy = record.RequestedBy,
            Status = record.Status,
            RequestedUtc = record.RequestedUtc,
            ReasonCode = record.ReasonCode,
            ReasonDetail = record.ReasonDetail,
            Reviewer = record.Reviewer,
            ReviewedUtc = record.ReviewedUtc,
            ReviewNotes = record.ReviewNotes,
            RoutedTo = DefaultApprovers
        };
    }
}
