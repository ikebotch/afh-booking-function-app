namespace AFH.Booking.Application.Models.Clients;

public sealed class DuplicateClientCaseResponse
{
    public string CaseId { get; init; } = default!;
    public string PrimaryTransactionRef { get; init; } = default!;
    public string DuplicateTransactionRef { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? Notes { get; init; }
    public string? RaisedBy { get; init; }
    public DateTime RaisedUtc { get; init; }
    public string? Resolution { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedUtc { get; init; }
}

public sealed class DownstreamUpdateResponse
{
    public string UpdateId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime? ProcessedUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class DownstreamUpdateReconciliationResponse
{
    public int RequestedCount { get; init; }
    public int RetriedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<DownstreamUpdateReconciliationItemResponse> Results { get; init; } = [];
}

public sealed class DownstreamUpdateReconciliationItemResponse
{
    public string UpdateId { get; init; } = default!;
    public string BookingId { get; init; } = default!;
    public string ChangeType { get; init; } = default!;
    public string PreviousStatus { get; init; } = default!;
    public string CurrentStatus { get; init; } = default!;
    public int AttemptCount { get; init; }
    public DateTime? ProcessedUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class AdviserProjectionSyncResult
{
    public int SyncedCount { get; init; }
    public int DiscoveredMeetingTopicCount { get; init; }
    public DateTime SyncedAtUtc { get; init; }
    public string Source { get; init; } = string.Empty;
}
