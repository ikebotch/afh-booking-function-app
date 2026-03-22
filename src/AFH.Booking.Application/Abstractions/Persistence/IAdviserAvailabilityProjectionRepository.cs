namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IAdviserAvailabilityProjectionRepository
{
    Task UpsertBusyBlockAsync(AdviserBusyBlockProjection block, CancellationToken ct);
    Task DeleteBusyBlockAsync(string adviserId, string providerEventId, DateTime syncedUtc, CancellationToken ct);
    Task<IReadOnlyList<AdviserBusyBlockProjection>> ListBusyBlocksAsync(string adviserId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<DateTime?> GetLastSyncedUtcAsync(string adviserId, CancellationToken ct);
}

public sealed class AdviserBusyBlockProjection
{
    public string Id { get; init; } = string.Empty;
    public string AdviserId { get; init; } = string.Empty;
    public string ProviderEventId { get; init; } = string.Empty;
    public string? CalendarId { get; init; }
    public string? Subject { get; init; }
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public bool IsCancelled { get; init; }
    public string? ChangeKey { get; init; }
    public string? ICalUId { get; init; }
    public DateTime LastSyncedUtc { get; init; }
    public string? SourceReceiptId { get; init; }
}
