using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Function.Http;
using AFH.Acs.Function.Services.Meetings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingLinkFunction(IMeetingWorkflowStore meetings)
{
    [Function("v1-meetings-link")]
    public async Task<HttpResponseData> CreateMeetingLinkAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/link")] HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<CreateMeetingLinkRequest>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.BookingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "bookingId is required.", ct, "VALIDATION_ERROR");

        var result = await meetings.CreateMeetingLinkAsync(payload, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }
}
