using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Function.Http;
using AFH.Acs.Function.Services.Transcription;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingTranscriptionFunction(ITranscriptionWorkflowService transcriptionService)
{
    [Function("v1-meetings-transcriptions-submit")]
    public async Task<HttpResponseData> SubmitAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meetings/{meetingId}/transcriptions")] HttpRequestData req,
        string meetingId,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<SubmitTranscriptionRequest>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ContentUrl))
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "contentUrl is required.", ct, "VALIDATION_ERROR");
        }

        var result = await transcriptionService.SubmitAsync(meetingId, payload, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }
}
