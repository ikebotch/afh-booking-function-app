using System.Collections.Concurrent;
using System.Security.Cryptography;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using Microsoft.Extensions.Configuration;

namespace AFH.Acs.Function.Services.Meetings;

public sealed class MeetingWorkflowStore(IConfiguration configuration) : IMeetingWorkflowStore
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, MeetingRecord> _meetingsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _meetingIdsByGroupId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _meetingIdsByTranscriptionJobId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MeetingRecordingResponse> _recordingsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public Task<MeetingScheduleResponse> CreateMeetingAsync(ScheduleMeetingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Start >= request.End)
            throw new ArgumentException("Start must be before End.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.AdviserId))
            throw new ArgumentException("AdviserId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.LeadId))
            throw new ArgumentException("LeadId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.MeetingType))
            throw new ArgumentException("MeetingType is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ClientEmail))
            throw new ArgumentException("ClientEmail is required.", nameof(request));

        var meetingId = NewId();
        var groupId = NewId();
        var joinBaseUrl = GetJoinBaseUrl();

        var record = new MeetingRecord
        {
            MeetingId = meetingId,
            GroupId = groupId,
            AdviserId = request.AdviserId.Trim(),
            LeadId = request.LeadId.Trim(),
            MeetingType = request.MeetingType.Trim(),
            Title = request.Title.Trim(),
            Start = request.Start.ToUniversalTime(),
            End = request.End.ToUniversalTime(),
            ClientEmail = request.ClientEmail.Trim(),
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim(),
            Status = "Scheduled"
        };

        _meetingsById[meetingId] = record;
        _meetingIdsByGroupId[groupId] = meetingId;

        return Task.FromResult(new MeetingScheduleResponse
        {
            MeetingId = meetingId,
            GroupId = groupId,
            JoinCode = groupId,
            ClientJoinUrl = $"{joinBaseUrl}/meeting/{groupId}?role=client",
            AdviserJoinUrl = $"{joinBaseUrl}/meeting/{groupId}?role=adviser",
            AdviserId = record.AdviserId,
            LeadId = record.LeadId,
            MeetingType = record.MeetingType,
            Title = record.Title,
            Start = record.Start,
            End = record.End,
            ClientEmail = record.ClientEmail,
            ClientName = record.ClientName
        });
    }

    public Task<MeetingDetailsResponse?> GetMeetingByIdAsync(string meetingId, CancellationToken ct = default)
        => Task.FromResult(SnapshotByMeetingId(RequireMeetingId(meetingId)));

    public Task<MeetingDetailsResponse?> GetMeetingByGroupIdAsync(string groupId, CancellationToken ct = default)
    {
        var meetingId = ResolveMeetingIdByGroupId(groupId);
        return Task.FromResult(meetingId is null ? null : SnapshotByMeetingId(meetingId));
    }

    public Task<MeetingConsentResponse> RecordConsentAsync(string groupId, bool consent, CancellationToken ct = default)
    {
        var meetingId = ResolveMeetingIdByGroupId(groupId) ?? throw new InvalidOperationException($"Meeting not found for GroupId={groupId}.");
        var now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            var record = RequireRecord(meetingId);
            record.ConsentToRecording = consent;
            record.ConsentTimestampUtc = consent ? now : null;
        }

        return Task.FromResult(new MeetingConsentResponse
        {
            MeetingId = meetingId,
            GroupId = groupId.Trim(),
            ConsentToRecording = consent,
            ConsentTimestampUtc = now
        });
    }

    public Task<JoinTokenResponse> IssueJoinTokenAsync(string groupId, JoinTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var record = RequireRecordByGroupId(groupId);
        var userId = NewId();
        var expiresOn = DateTimeOffset.UtcNow.Add(TokenLifetime);

        return Task.FromResult(new JoinTokenResponse
        {
            MeetingId = record.MeetingId,
            GroupId = record.GroupId,
            UserId = userId,
            Token = BuildToken(record.GroupId, request.DisplayName, request.Role, userId),
            ExpiresOn = expiresOn,
            DisplayName = request.DisplayName.Trim()
        });
    }

    public Task<IdentityTokenResponse> IssueIdentityTokenAsync(CancellationToken ct = default)
    {
        var identityId = NewId();

        return Task.FromResult(new IdentityTokenResponse
        {
            IdentityId = identityId,
            Token = BuildToken(identityId, "identity", "voip", NewId()),
            ExpiresOn = DateTimeOffset.UtcNow.Add(TokenLifetime)
        });
    }

    public Task<MeetingLinkResponse> CreateMeetingLinkAsync(CreateMeetingLinkRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.BookingId))
            throw new ArgumentException("BookingId is required.", nameof(request));

        var joinBaseUrl = GetJoinBaseUrl();
        var joinCode = NewId();

        return Task.FromResult(new MeetingLinkResponse
        {
            BookingId = request.BookingId.Trim(),
            GroupId = joinCode,
            JoinCode = joinCode,
            JoinUrl = $"{joinBaseUrl}/meeting/{joinCode}"
        });
    }

    public Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var record = RequireRecordByMeetingOrGroup(request.MeetingId, request.GroupId);
        var active = record.Recordings.FirstOrDefault(item => item.RecordingEndUtc is null);
        if (active is not null)
        {
            return Task.FromResult(active);
        }

        var recordingId = NewId();
        var recording = new MeetingRecordingResponse
        {
            RecordingId = recordingId,
            MeetingId = record.MeetingId,
            GroupId = record.GroupId,
            BlobName = string.IsNullOrWhiteSpace(request.BlobName) ? $"recordings/{recordingId}.wav" : request.BlobName.Trim(),
            BlobUrl = $"{GetJoinBaseUrl().TrimEnd('/')}/recordings/{recordingId}",
            RecordingStartUtc = DateTimeOffset.UtcNow,
            RecordingEndUtc = null,
            DurationSeconds = null
        };

        lock (_sync)
        {
            record.Recordings.Add(recording);
            _recordingsById[recording.RecordingId] = recording;
        }

        return Task.FromResult(recording);
    }

    public Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RecordingId))
            throw new ArgumentException("RecordingId is required.", nameof(request));

        var recording = _recordingsById.TryGetValue(request.RecordingId.Trim(), out var found)
            ? found
            : throw new InvalidOperationException($"Recording not found for RecordingId={request.RecordingId}.");

        lock (_sync)
        {
            if (recording.RecordingEndUtc is null)
            {
                var endUtc = DateTimeOffset.UtcNow;
                recording.RecordingEndUtc = endUtc;
                recording.DurationSeconds = (int)Math.Max(0, Math.Round((endUtc - recording.RecordingStartUtc).TotalSeconds));
            }
        }

        return Task.FromResult(recording);
    }

    public Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            return Task.FromResult<IReadOnlyList<MeetingRecordingResponse>>(
                _recordingsById.Values.OrderByDescending(item => item.RecordingStartUtc).ToArray());
        }

        var record = RequireRecord(meetingId);
        return Task.FromResult<IReadOnlyList<MeetingRecordingResponse>>(
            record.Recordings.OrderByDescending(item => item.RecordingStartUtc).ToArray());
    }

    public Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recordingId))
            return Task.FromResult<MeetingRecordingResponse?>(null);

        return Task.FromResult(_recordingsById.TryGetValue(recordingId.Trim(), out var recording) ? recording : null);
    }

    public Task AttachTranscriptionAsync(string? meetingId, TranscriptionJobResponse transcription, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transcription);

        if (string.IsNullOrWhiteSpace(meetingId))
            return Task.CompletedTask;

        lock (_sync)
        {
            if (_meetingsById.TryGetValue(meetingId.Trim(), out var record))
            {
                record.Transcription = new MeetingTranscriptionResponse
                {
                    TranscriptionId = transcription.JobId,
                    Language = transcription.Locale ?? "en-GB",
                    FullText = string.Empty,
                    SummaryText = null
                };

                if (!string.IsNullOrWhiteSpace(transcription.JobId))
                {
                    _meetingIdsByTranscriptionJobId[transcription.JobId] = record.MeetingId;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task AttachTranscriptContentAsync(
        string jobId,
        string? transcriptText,
        string? speakerFormattedTranscript,
        string? transcriptFileName,
        string? transcriptFileUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Task.CompletedTask;

        if (!_meetingIdsByTranscriptionJobId.TryGetValue(jobId.Trim(), out var meetingId))
            return Task.CompletedTask;

        lock (_sync)
        {
            if (_meetingsById.TryGetValue(meetingId, out var record))
            {
                record.Transcription = new MeetingTranscriptionResponse
                {
                    TranscriptionId = jobId.Trim(),
                    Language = record.Transcription?.Language ?? "en-GB",
                    FullText = transcriptText ?? string.Empty,
                    SummaryText = speakerFormattedTranscript
                };
            }
        }

        return Task.CompletedTask;
    }

    private MeetingDetailsResponse? SnapshotByMeetingId(string meetingId)
    {
        if (!_meetingsById.TryGetValue(meetingId, out var record))
            return null;

        lock (_sync)
        {
            return record.Snapshot();
        }
    }

    private MeetingRecord RequireRecordByGroupId(string? groupId)
    {
        var meetingId = ResolveMeetingIdByGroupId(groupId) ?? throw new InvalidOperationException($"Meeting not found for GroupId={groupId}.");
        return RequireRecord(meetingId);
    }

    private MeetingRecord RequireRecordByMeetingOrGroup(string? meetingId, string? groupId)
    {
        if (!string.IsNullOrWhiteSpace(meetingId))
            return RequireRecord(meetingId.Trim());

        return RequireRecordByGroupId(groupId);
    }

    private MeetingRecord RequireRecord(string meetingId)
    {
        if (!_meetingsById.TryGetValue(meetingId, out var record))
            throw new InvalidOperationException($"Meeting not found for MeetingId={meetingId}.");

        return record;
    }

    private string RequireMeetingId(string? meetingId)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
            throw new ArgumentException("meetingId is required.", nameof(meetingId));

        return meetingId.Trim();
    }

    private string? ResolveMeetingIdByGroupId(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return null;

        return _meetingIdsByGroupId.TryGetValue(groupId.Trim(), out var meetingId) ? meetingId : null;
    }

    private string GetJoinBaseUrl()
        => (configuration["Frontend:JoinBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string BuildToken(string part1, string part2, string part3, string part4)
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var random = Convert.ToBase64String(bytes).TrimEnd('=');
        return $"{part1[..Math.Min(8, part1.Length)]}.{part2[..Math.Min(8, part2.Length)]}.{part3[..Math.Min(8, part3.Length)]}.{part4[..Math.Min(8, part4.Length)]}.{random}";
    }

    private sealed class MeetingRecord
    {
        public string MeetingId { get; init; } = string.Empty;
        public string GroupId { get; init; } = string.Empty;
        public string AdviserId { get; init; } = string.Empty;
        public string LeadId { get; init; } = string.Empty;
        public string MeetingType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTimeOffset Start { get; init; }
        public DateTimeOffset End { get; init; }
        public string ClientEmail { get; init; } = string.Empty;
        public string? ClientName { get; init; }
        public string Status { get; set; } = "Scheduled";
        public bool ConsentToRecording { get; set; }
        public DateTimeOffset? ConsentTimestampUtc { get; set; }
        public List<MeetingRecordingResponse> Recordings { get; } = [];
        public MeetingTranscriptionResponse? Transcription { get; set; }

        public MeetingDetailsResponse Snapshot()
            => new()
            {
                MeetingId = MeetingId,
                GroupId = GroupId,
                AdviserId = AdviserId,
                LeadId = LeadId,
                MeetingType = MeetingType,
                Title = Title,
                Start = Start,
                End = End,
                ClientEmail = ClientEmail,
                ClientName = ClientName,
                ConsentToRecording = ConsentToRecording,
                ConsentTimestampUtc = ConsentTimestampUtc,
                Status = Status,
                Attendees = [],
                Recordings = Recordings.OrderByDescending(item => item.RecordingStartUtc).ToArray(),
                Transcription = Transcription
            };
    }
}
