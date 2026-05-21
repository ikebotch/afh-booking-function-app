namespace AFH.Booking.Application.Abstractions.Clients;

public interface IAdviserProjectionSyncService
{
    Task<AdviserProjectionSyncResult> SyncAsync(CancellationToken ct);
}

public sealed class AdviserProjectionSyncResult
{
    public int SyncedCount { get; init; }
    public DateTime SyncedAtUtc { get; init; }
    public string Source { get; init; } = string.Empty;
}
