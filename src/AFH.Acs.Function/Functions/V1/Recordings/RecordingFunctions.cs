using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Recordings;

public sealed class RecordingFunctions(IMeetingRecordingService recordings)
{
    [Function("v1-recordings-start")]
    public async Task<HttpResponseData> StartRecordingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/recordings/start")] HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<StartRecordingRequest>(ct);
        if (payload is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid payload.", ct, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(payload.MeetingId) && string.IsNullOrWhiteSpace(payload.GroupId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "meetingId or groupId is required.", ct, "VALIDATION_ERROR");

        var result = await recordings.StartRecordingAsync(payload, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-recordings-stop")]
    public async Task<HttpResponseData> StopRecordingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/recordings/stop")] HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<StopRecordingRequest>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.RecordingId))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "RecordingId is required.", ct, "VALIDATION_ERROR");

        var result = await recordings.StopRecordingAsync(payload, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-recordings-list")]
    public async Task<HttpResponseData> ListRecordingsAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/recordings")] HttpRequestData req,
        CancellationToken ct)
    {
        var meetingId = req.Query("meetingId");
        var items = await recordings.ListRecordingsAsync(meetingId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new RecordingListResponse
        {
            MeetingId = meetingId,
            Items = items
        }, cancellationToken: ct);
        return response;
    }

    [Function("v1-recordings-get")]
    public async Task<HttpResponseData> GetRecordingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/recordings/{recordingId}")] HttpRequestData req,
        string recordingId,
        CancellationToken ct)
    {
        var result = await recordings.GetRecordingAsync(recordingId, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Recording not found.", ct, "NOT_FOUND")
            : await WriteOkAsync(req, result, ct);
    }

    private static async Task<HttpResponseData> WriteOkAsync<T>(HttpRequestData req, T body, CancellationToken ct)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(body, cancellationToken: ct);
        return response;
    }
}
