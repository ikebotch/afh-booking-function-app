using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class UpsertMeetingTypeFunction
{
    private readonly IMeetingTypeRepository _types;
    private readonly IUnitOfWork _uow;

    public UpsertMeetingTypeFunction(
        IMeetingTypeRepository types,
        IUnitOfWork uow)
    {
        _types = types;
        _uow = uow;
    }

    [Function("Config_UpsertMeetingType")]
    [BookingOpenApiOperation(
        "Config",
        "Create or update meeting type",
        RequestBodyType = typeof(UpsertMeetingTypeRequest),
        ResponseType = typeof(MeetingTypeDto))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "v1/config/meeting-types/{code}")]
        HttpRequestData req,
        string code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "code is required.", ct, "Validation");

        var body = await req.ReadJsonAsync<UpsertMeetingTypeRequest>(ct) ?? new UpsertMeetingTypeRequest();
        var meetingType = await _types.UpsertAsync(new MeetingTypeUpsert
        {
            Code = code,
            Label = body.Label ?? code,
            IsDefault = body.IsDefault,
            IsActive = body.IsActive,
            DefaultDurationMinutes = body.DefaultDurationMinutes,
            SortOrder = body.SortOrder,
            ChangedUtc = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);

        return await req.OkJsonAsync(new MeetingTypeDto
        {
            Code = meetingType.Code,
            Label = meetingType.Label,
            IsDefault = meetingType.IsDefault,
            DefaultDurationMinutes = meetingType.DefaultDurationMinutes
        }, ct);
    }
}
