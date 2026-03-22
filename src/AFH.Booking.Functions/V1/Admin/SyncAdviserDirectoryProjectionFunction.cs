using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Functions.Http;

namespace AFH.Booking.Functions.V1.Admin;

public sealed class SyncAdviserDirectoryProjectionFunction
{
    private readonly IAdviserDirectorySyncService _syncService;
    private readonly IUnitOfWork _uow;

    public SyncAdviserDirectoryProjectionFunction(
        IAdviserDirectorySyncService syncService,
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
