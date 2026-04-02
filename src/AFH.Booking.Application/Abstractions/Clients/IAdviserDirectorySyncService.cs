namespace AFH.Booking.Application.Abstractions.Clients;

public interface IAdviserDirectorySyncService
{
    Task<AdviserDirectorySyncResult> SyncAsync(CancellationToken ct);
}

public sealed class AdviserDirectorySyncResult
{
    public int SyncedCount { get; init; }
    public DateTime SyncedAtUtc { get; init; }
    public string Source { get; init; } = string.Empty;
}
