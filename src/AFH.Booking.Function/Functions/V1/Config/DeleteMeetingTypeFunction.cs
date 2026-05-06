using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class DeleteMeetingTypeFunction
{
    private readonly IMeetingTypeRepository _types;
    private readonly IUnitOfWork _uow;

    public DeleteMeetingTypeFunction(
        IMeetingTypeRepository types,
        IUnitOfWork uow)
    {
        _types = types;
        _uow = uow;
    }

    [Function("Config_DeleteMeetingType")]
    [BookingOpenApiOperation("Config", "Delete meeting type")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/config/meeting-types/{code}")]
        HttpRequestData req,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "code is required.", ct, "Validation");

        var deleted = await _types.DeactivateAsync(code, DateTime.UtcNow, ct);
        if (!deleted)
            return await req.ProblemAsync(HttpStatusCode.NotFound, $"Meeting type '{code}' was not found.", ct, "NotFound");

        await _uow.SaveChangesAsync(ct);
        return await req.OkJsonAsync(new { code = code.Trim(), deleted = true }, ct);
    }
}
