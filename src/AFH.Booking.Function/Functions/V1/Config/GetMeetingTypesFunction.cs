using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;

namespace AFH.Booking.Function.Functions.V1.Config;

[BookingOpenApiTag("Config")]
public sealed class GetMeetingTypesFunction
{
    private readonly IMeetingTypeRepository _types;

    public GetMeetingTypesFunction(IMeetingTypeRepository types)
    {
        _types = types;
    }

    [Function("Config_GetMeetingTypes")]
    [BookingOpenApiOperation(
        "Config",
        "Get meeting types",
        ResponseType = typeof(MeetingTypesResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/config/meeting-types")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var meetingTypes = (await _types.ListActiveAsync(ct))
            .Select(x => new MeetingTypeDto
            {
                Code = x.Code.Trim(),
                Label = string.IsNullOrWhiteSpace(x.Label) ? x.Code.Trim() : x.Label.Trim(),
                IsDefault = x.IsDefault,
                DefaultDurationMinutes = x.DefaultDurationMinutes is > 0 ? x.DefaultDurationMinutes : null
            })
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await req.OkJsonAsync(new MeetingTypesResponse
        {
            Source = "MeetingTypes",
            MeetingTypes = meetingTypes
        }, ct);
    }
}