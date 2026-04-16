using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Models;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace AFH.Acs.Function.Functions.V1.Meetings;

public sealed class MeetingLifecycleFunctions(IMeetingSessionService meetings)
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

        var result = await meetings.ScheduleAsync(new ScheduleMeetingCommand
        {
            AdviserId = payload.AdviserId,
            LeadId = payload.LeadId,
            MeetingType = payload.MeetingType,
            Title = payload.Title,
            Start = payload.Start,
            End = payload.End,
            ClientEmail = payload.ClientEmail,
            ClientName = payload.ClientName
        }, ct);

        var session = await meetings.GetByIdAsync(result.MeetingId, ct);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(MapSchedule(result, session, payload), cancellationToken: ct);
        return response;
    }

    [Function("v1-meetings-get-by-id")]
    public async Task<HttpResponseData> GetByIdAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/meetings/{meetingId}")] HttpRequestData req,
        string meetingId,
        CancellationToken ct)
    {
        var result = await meetings.GetByIdAsync(meetingId, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Meeting not found.", ct, "NOT_FOUND")
            : await WriteOkAsync(req, MapDetails(result), ct);
    }

    [Function("v1-meetings-get-by-group")]
    public async Task<HttpResponseData> GetByGroupAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/meet/{groupId}")] HttpRequestData req,
        string groupId,
        CancellationToken ct)
    {
        var result = await meetings.GetByGroupIdAsync(groupId, ct);
        return result is null
            ? await req.ProblemAsync(HttpStatusCode.NotFound, "Meeting not found.", ct, "NOT_FOUND")
            : await WriteOkAsync(req, MapDetails(result), ct);
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

        var updated = await meetings.RecordConsentAsync(new RecordMeetingConsentCommand
        {
            GroupId = groupId,
            Consent = payload.Consent
        }, ct);
        return await WriteOkAsync(req, new MeetingConsentResponse
        {
            MeetingId = updated.MeetingId,
            GroupId = updated.GroupId,
            ConsentToRecording = updated.ConsentToRecording,
            ConsentTimestampUtc = updated.ConsentTimestampUtc ?? DateTimeOffset.UtcNow
        }, ct);
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

        var result = await meetings.IssueJoinTokenAsync(new IssueJoinTokenCommand
        {
            GroupId = groupId,
            DisplayName = payload.DisplayName,
            Role = payload.Role
        }, ct);

        return await WriteOkAsync(req, new JoinTokenResponse
        {
            MeetingId = result.MeetingId,
            GroupId = result.GroupId,
            UserId = result.UserId,
            Token = result.Token,
            ExpiresOn = result.ExpiresOn,
            DisplayName = result.DisplayName
        }, ct);
    }

    private static MeetingScheduleResponse MapSchedule(MeetingSessionScheduleResult scheduled, MeetingSession? session, ScheduleMeetingRequest request)
        => new()
        {
            MeetingId = scheduled.MeetingId,
            GroupId = scheduled.GroupId,
            JoinCode = scheduled.JoinCode,
            ClientJoinUrl = scheduled.ClientJoinUrl,
            AdviserJoinUrl = scheduled.AdviserJoinUrl,
            AdviserId = session?.AdviserId ?? request.AdviserId,
            LeadId = session?.LeadId ?? request.LeadId,
            MeetingType = session?.MeetingType ?? request.MeetingType,
            Title = session?.Title ?? request.Title,
            Start = session?.StartUtc ?? request.Start,
            End = session?.EndUtc ?? request.End,
            ClientEmail = session?.ClientEmail ?? request.ClientEmail,
            ClientName = session?.ClientName ?? request.ClientName
        };

    private static MeetingDetailsResponse MapDetails(MeetingSession session)
        => new()
        {
            MeetingId = session.MeetingId,
            GroupId = session.GroupId,
            AdviserId = session.AdviserId,
            AdviserName = session.AdviserName,
            LeadId = session.LeadId,
            MeetingType = session.MeetingType,
            Title = session.Title,
            Start = session.StartUtc,
            End = session.EndUtc,
            ClientEmail = session.ClientEmail,
            ClientName = session.ClientName,
            ConsentToRecording = session.ConsentToRecording,
            ConsentTimestampUtc = session.ConsentTimestampUtc,
            Status = session.Status.ToString(),
            Attendees = session.Attendees.Select(attendee => new MeetingAttendeeResponse
            {
                Email = attendee.Email,
                Role = attendee.Role,
                ResponseStatus = attendee.ResponseStatus,
                ResponseTimeUtc = attendee.ResponseTimeUtc
            }).ToArray(),
            Recordings = session.Recordings.Select(recording => new MeetingRecordingResponse
            {
                RecordingId = recording.RecordingId,
                MeetingId = session.MeetingId,
                GroupId = session.GroupId,
                BlobName = recording.BlobName,
                BlobUrl = recording.BlobUrl,
                RecordingStartUtc = recording.RecordingStartUtc,
                RecordingEndUtc = recording.RecordingEndUtc,
                DurationSeconds = recording.DurationSeconds
            }).ToArray(),
            Transcription = session.Transcription is null
                ? null
                : new MeetingTranscriptionResponse
                {
                    TranscriptionId = session.Transcription.TranscriptionId,
                    Language = session.Transcription.Language,
                    FullText = session.Transcription.FullText,
                    SummaryText = session.Transcription.SummaryText
                }
        };

    private static async Task<HttpResponseData> WriteOkAsync<T>(HttpRequestData req, T body, CancellationToken ct)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(body, cancellationToken: ct);
        return response;
    }
}
