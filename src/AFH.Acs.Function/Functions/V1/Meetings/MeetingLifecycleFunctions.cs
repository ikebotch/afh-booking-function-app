using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Function.Http;
using AFH.Acs.Function.Services.Meetings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingLifecycleFunctions(IMeetingWorkflowStore meetings)
{
    [Function("v1-meetings-create")]
    public async Task<HttpResponseData> CreateMeetingAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/create")] HttpRequestData req,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<ScheduleMeetingRequest>(ct);
        if (payload is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid payload.", ct, "VALIDATION_ERROR");

        if (string.IsNullOrWhiteSpace(payload.AdviserId)
            || string.IsNullOrWhiteSpace(payload.LeadId)
            || string.IsNullOrWhiteSpace(payload.MeetingType)
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.ClientEmail))
        {
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "adviserId, leadId, meetingType, title, and clientEmail are required.", ct, "VALIDATION_ERROR");
        }

        var result = await meetings.CreateMeetingAsync(payload, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result, cancellationToken: ct);
        return response;
    }

    [Function("v1-meetings-get-by-id")]
    public async Task<HttpResponseData> GetByIdAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/meetings/{meetingId}")] HttpRequestData req,
        string meetingId,
        CancellationToken ct)
    {
        var result = await meetings.GetMeetingByIdAsync(meetingId, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Meeting not found.", ct, "NOT_FOUND")
            : await WriteOkAsync(req, result, ct);
    }

    [Function("v1-meetings-get-by-group")]
    public async Task<HttpResponseData> GetByGroupAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/meet/{groupId}")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var result = await meetings.GetMeetingByGroupIdAsync(groupId, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Meeting not found.", ct, "NOT_FOUND")
            : await WriteOkAsync(req, result, ct);
    }

    [Function("v1-meetings-consent")]
    public async Task<HttpResponseData> RecordConsentAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/{groupId}/consent")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<MeetingConsentRequest>(ct);
        if (payload is null)
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "Invalid payload.", ct, "VALIDATION_ERROR");

        var result = await meetings.RecordConsentAsync(groupId, payload.Consent, ct);
        return await WriteOkAsync(req, result, ct);
    }

    [Function("v1-meetings-join-token")]
    public async Task<HttpResponseData> IssueJoinTokenAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/meet/{groupId}/join-token")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var payload = await req.ReadJsonAsync<JoinTokenRequest>(ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.DisplayName))
            return await req.ProblemAsync(HttpStatusCode.BadRequest, "displayName is required.", ct, "VALIDATION_ERROR");

        var result = await meetings.IssueJoinTokenAsync(groupId, payload, ct);
        return await WriteOkAsync(req, result, ct);
    }

    private static async Task<HttpResponseData> WriteOkAsync<T>(HttpRequestData req, T body, CancellationToken ct)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(body, cancellationToken: ct);
        return response;
    }
}
