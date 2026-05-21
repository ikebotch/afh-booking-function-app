using AFH.Booking.Application.Models.Clients;

namespace AFH.Booking.Application.Abstractions.Clients;

public interface IAdviserProjectionSyncService
{
    Task<AdviserProjectionSyncResult> SyncAsync(CancellationToken ct);
}
