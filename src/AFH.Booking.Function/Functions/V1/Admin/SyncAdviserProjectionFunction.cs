using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Admin;

[BookingOpenApiTag("Internal/Admin")]
public sealed class SyncAdviserProjectionFunction
{
    private readonly IAdviserProjectionSyncService _syncService;
    private readonly IUnitOfWork _uow;

    public SyncAdviserProjectionFunction(
        IAdviserProjectionSyncService syncService,
        IUnitOfWork uow)
    {
        _syncService = syncService;
        _uow = uow;
    }

    [Function("Admin_SyncAdviserDirectoryProjection")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/admin/advisers/projection/sync")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var result = await _syncService.SyncAsync(ct);
        await _uow.SaveChangesAsync(ct);
        return await req.OkJsonAsync(result, ct);
    }
}
