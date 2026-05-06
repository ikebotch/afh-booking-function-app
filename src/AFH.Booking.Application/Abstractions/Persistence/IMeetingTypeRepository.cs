namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IMeetingTypeRepository
{
    Task<IReadOnlyList<MeetingTypeRecord>> ListActiveAsync(CancellationToken ct);
    Task<MeetingTypeRecord> UpsertAsync(MeetingTypeUpsert change, CancellationToken ct);
    Task<bool> DeactivateAsync(string code, DateTime changedUtc, CancellationToken ct);
}

public sealed class MeetingTypeRecord
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public int? DefaultDurationMinutes { get; init; }
    public int SortOrder { get; init; }
}

public sealed class MeetingTypeUpsert
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public int? DefaultDurationMinutes { get; init; }
    public int SortOrder { get; init; }
    public DateTime ChangedUtc { get; init; }
}
