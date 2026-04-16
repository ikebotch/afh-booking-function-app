using AFH.Acs.Recorder.DTOs;
using AFH.Acs.Recorder.Functions.Meetings;
using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Repositories.Persistence;
using AFH.Acs.Recorder.Models;
using AFH.Acs.Recorder.Services.Interface;
using AFH.Acs.Recorder.Services.Lookup;
using AutoMapper;
using Azure.Communication.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Recorder.Services;

public class MeetingService : IMeetingService
{
    private readonly IMeetingRepository _repo;
    private readonly CommunicationIdentityClient _identityClient;
    private readonly IConfiguration _config;
    private readonly IMapper _mapper;
    private readonly ILogger<MeetingService> _logger;
    private readonly IAdviserService _adviserService;
    private readonly IGraphClient _graphClient;
    public MeetingService(
         IMeetingRepository repo,
         CommunicationIdentityClient identityClient,
         IConfiguration config,
         IMapper mapper,
           IGraphClient graphClient,
           IAdviserService adviserService,
         ILogger<MeetingService> logger)
    {
        _repo = repo;
        _identityClient = identityClient;
        _config = config;
        _mapper = mapper;
        _logger = logger;
        _adviserService = adviserService;
        _graphClient = graphClient;
    }

    /// <summary>
    /// Schedules a meeting:
    /// - creates MeetingEntity from request
    /// - assigns MeetingId + GroupId + GraphEventId placeholder
    /// - persists to DB via repository
    /// - returns MeetingScheduleResponse with join URLs
    /// </summary>
    public async Task<MeetingScheduleResponse> ScheduleAsync(
        MeetingScheduleRequest request,
        CancellationToken ct = default)
    {

        //var adviser = await _adviserService.GetAdviserAsync(request.AdviserId, ct);

        request.AdviserId = "ADV002";
        var adviser = await _adviserService.GetAdviserAsync(request.AdviserId, ct);
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        // Basic guardrails
        if (request.Start >= request.End)
            throw new ArgumentException("Start must be before End.", nameof(request));

        var groupId = Guid.NewGuid().ToString("N");
        var meetingId = Guid.NewGuid().ToString("N");

        // Later: replace with real Graph event creation
        var graphEventId = Guid.NewGuid().ToString("N");

        var joinBaseUrl = _config["Frontend:JoinBaseUrl"] ?? "http://localhost:5173";
        var joinCode = groupId;

        var clientJoinUrl = $"{joinBaseUrl}/meeting/{joinCode}?role=client";
        var adviserJoinUrl = $"{joinBaseUrl}/meeting/{joinCode}?role=adviser";



        _logger.LogInformation(
            "Scheduling meeting {MeetingId} (GroupId={GroupId}) for Adviser {AdviserId}, Lead {LeadId}",
            meetingId, groupId, request.AdviserId, request.LeadId);


        var graphRequest = new GraphMeetingCreateRequest
        {
            AdviserEmail = adviser.Email,
            AdviserName = adviser.Name,
            ClientEmail = request.ClientEmail,
            ClientName = request.ClientName,
            Subject = request.MeetingType,
            Start = request.Start,
            End = request.End,
            JoinUrl = adviserJoinUrl,
            Location = $"Online – AFH Client Meeting ({adviserJoinUrl})"
        }
        ;

        var calendarEventId = await _graphClient.CreateAdviserMeetingAsync(graphRequest, ct)
                                           .ConfigureAwait(false);


        // Map request -> entity
        var entity = _mapper.Map<MeetingEntity>(request);
        entity.MeetingId = meetingId;
        entity.GroupId = groupId;
        entity.GraphEventId = calendarEventId ?? graphEventId;
        entity.CreatedAtUtc = DateTime.UtcNow;
    
        // Persist
        await _repo.InsertAsync(entity, ct);

        // Map back -> response and decorate with join URLs
        var response = _mapper.Map<MeetingScheduleResponse>(entity);
        response.MeetingId = meetingId;
        response.GroupId = groupId;
        response.GraphEventId = graphEventId;
        response.ClientJoinUrl = clientJoinUrl;
        response.AdviserJoinUrl = adviserJoinUrl;
        response.JoinCode = joinCode;

        return response;
    }



    public async Task<MeetingDetailsDto?> GetByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByGroupIdAsync(groupId, ct).ConfigureAwait(false);
        return entity is null ? null : _mapper.Map<MeetingDetailsDto>(entity);
    }


    public async Task<MeetingDetailsDto?> GetMeetingByIdAsync(string meetingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            throw new ArgumentException("meetingId is required.", nameof(meetingId));
        var entity = _repo.GetByIdAsync(meetingId, ct);
        return entity is null ? null : _mapper.Map<MeetingDetailsDto>(entity);
    }

    public Task<MeetingDetailsDto?> GetMeetingByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("groupId is required.", nameof(groupId));

        return _repo.GetByGroupIdAsync(groupId, ct);
    }

    /// <summary>
    /// Records consent against the meeting identified by groupId.
    /// </summary>
    public async Task<MeetingConsentResponse> RecordConsentAsync(
        string groupId,
        MeetingConsentRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("groupId is required.", nameof(groupId));

        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var now = DateTimeOffset.UtcNow;

        _logger.LogInformation("Recording consent={Consent} for GroupId={GroupId} at {Now}",
            request.Consent, groupId, now);

        // Let the repository handle locating and updating the entity
        var updated = await _repo.UpdateConsentByGroupIdAsync(
            groupId,
            request.Consent,
            now,
            ct);

        if (updated is null)
        {
            throw new InvalidOperationException($"Meeting not found for GroupId={groupId}");
        }

        return new MeetingConsentResponse
        {
            MeetingId = updated.MeetingId,
            GroupId = updated.GroupId,
            ConsentToRecording = updated.ConsentToRecording,
            ConsentTimestampUtc = now
        };
    }

    /// <summary>
    /// Issues an ACS join token for a user joining a meeting, given groupId.
    /// </summary>
    public async Task<JoinTokenResponse> IssueJoinTokenAsync(
           string groupId,
           JoinTokenRequest request,
           CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("groupId is required", nameof(groupId));

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ArgumentException("DisplayName is required", nameof(request.DisplayName));

        // 1) Look up the meeting by groupId
        var meeting = await _repo.GetByGroupIdAsync(groupId, ct);
        if (meeting is null)
        {
            _logger.LogWarning("IssueJoinTokenAsync: no meeting found for GroupId {GroupId}", groupId);
            throw new InvalidOperationException("Meeting not found for groupId.");
        }

        // Optionally validate status / time, etc.
        // if (meeting.Status == "Cancelled") throw new InvalidOperationException("Meeting is cancelled.");

        // 2) Create ACS identity and VoIP token
        _logger.LogInformation("IssueJoinTokenAsync: creating ACS identity for GroupId {GroupId}", groupId);

        var userResult = await _identityClient.CreateUserAsync(cancellationToken: ct);
        var user = userResult.Value;

        var tokenResult = await _identityClient.GetTokenAsync(
            user,
            new[] { CommunicationTokenScope.VoIP },
            ct);

        var token = tokenResult.Value;

        _logger.LogInformation(
            "IssueJoinTokenAsync: issued token for UserId {UserId} expiring at {ExpiresOn}",
            user.Id,
            token.ExpiresOn);

        // 3) Map to response
        var response = new JoinTokenResponse
        {
            MeetingId = meeting.MeetingId,
            GroupId = meeting.GroupId,
            UserId = user.Id,
            Token = token.Token,
            ExpiresOn = token.ExpiresOn,
            DisplayName = request.DisplayName
        };

        return response;
    }
}