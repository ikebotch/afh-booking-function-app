namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IMeetingTopicRepository
{
    Task<IReadOnlyList<MeetingTopicRecord>> ListActiveAsync(CancellationToken ct);
    Task<MeetingTopicRecord> UpsertAsync(MeetingTopicUpsert change, CancellationToken ct);
    Task<bool> DeactivateAsync(string code, DateTime changedUtc, CancellationToken ct);
}

public sealed class MeetingTopicRecord
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public int SortOrder { get; init; }
}

public sealed class MeetingTopicUpsert
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public int SortOrder { get; init; }
    public DateTime ChangedUtc { get; init; }
}