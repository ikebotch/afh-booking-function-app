using AFH.Acs.Application.Abstractions.Advisers;
using AFH.Acs.Application.Abstractions.Identity;
using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Application.Models;
using AFH.Acs.Application.Services.Identity;
using AFH.Acs.Application.Services.Meetings;
using AFH.Acs.Application.Services.Transcription;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Infrastructure.Recordings;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Acs.Tests;

public sealed class TranscriptionWorkflowServiceTests
{
    [Fact]
    public async Task Consumer_Flow_Covers_Meeting_Recording_And_Transcription()
    {
        var sessions = new FakeMeetingSessionRepository();
        var recordings = new FakeMeetingRecordingRepository();
        var transcriptionRepository = new FakeMeetingTranscriptionRepository();
        var adviserProvider = new FakeAdviserInfoProvider();
        var joinTokens = new FakeJoinTokenIssuer();
        var speechClient = new FakeSpeechTranscriptionClient();

        var meetingService = new MeetingSessionService(
            sessions,
            joinTokens,
            adviserProvider,
            NullLogger<MeetingSessionService>.Instance,
            "https://meetings.example");

        var linkService = new MeetingLinkService("https://meetings.example");
        var identityService = new IdentityTokenService(joinTokens);
        var recordingService = new MetadataMeetingRecordingService(sessions, recordings);
        var transcriptionService = new TranscriptionWorkflowService(speechClient, transcriptionRepository, sessions);

        var scheduled = await meetingService.ScheduleAsync(new ScheduleMeetingCommand
        {
            AdviserId = "adv-123",
            LeadId = "lead-456",
            MeetingType = "Review",
            Title = "Quarterly review",
            Start = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            ClientEmail = "client@example.com",
            ClientName = "Client Example"
        });

        var identity = await identityService.IssueAsync();
        var joinToken = await meetingService.IssueJoinTokenAsync(new IssueJoinTokenCommand
        {
            GroupId = scheduled.GroupId,
            DisplayName = "Client Example",
            Role = "Client"
        });
        var link = await linkService.CreateAsync(new CreateMeetingLinkCommand
        {
            BookingId = "booking-123"
        });

        var startedRecording = await recordingService.StartRecordingAsync(new StartRecordingRequest
        {
            MeetingId = scheduled.MeetingId
        });

        var stoppedRecording = await recordingService.StopRecordingAsync(new StopRecordingRequest
        {
            RecordingId = startedRecording.RecordingId
        });

        var submitted = await transcriptionService.SubmitAsync(
            scheduled.MeetingId,
            new SubmitTranscriptionRequest
            {
                ContentUrl = "https://storage.example/recordings/meeting-42.wav",
                DisplayName = "Quarterly review",
                Locale = "en-GB",
                Settings = new TranscriptionJobSettings
                {
                    DiarizationEnabled = true,
                    WordLevelTimestampsEnabled = true
                }
            });

        var status = await transcriptionService.GetStatusAsync(submitted.JobId);
        var files = await transcriptionService.GetFilesAsync(submitted.JobId);
        var content = await transcriptionService.GetContentAsync(submitted.JobId);
        var speakerTranscript = await transcriptionService.GetSpeakerFormattedTranscriptAsync(submitted.JobId);

        await transcriptionService.CancelAsync(submitted.JobId);
        await transcriptionService.DeleteAsync(submitted.JobId);

        var storedSession = await sessions.GetByIdAsync(scheduled.MeetingId);

        Assert.False(string.IsNullOrWhiteSpace(scheduled.MeetingId));
        Assert.False(string.IsNullOrWhiteSpace(scheduled.GroupId));
        Assert.Equal(scheduled.GroupId, scheduled.JoinCode);
        Assert.NotNull(storedSession);
        Assert.Equal(scheduled.MeetingId, storedSession!.MeetingId);
        Assert.Equal(scheduled.GroupId, storedSession.GroupId);
        Assert.Equal("adv-123", storedSession!.AdviserId);
        Assert.Equal("Adviser Example", storedSession.AdviserName);
        Assert.False(string.IsNullOrWhiteSpace(identity.Token));
        Assert.Equal(scheduled.MeetingId, joinToken.MeetingId);
        Assert.Equal("booking-123", link.BookingId);
        Assert.Equal(scheduled.MeetingId, startedRecording.MeetingId);
        Assert.Equal(scheduled.GroupId, startedRecording.GroupId);
        Assert.NotNull(stoppedRecording.RecordingEndUtc);
        Assert.Equal("Running", submitted.Status);
        Assert.Equal("Succeeded", status.Status);
        Assert.Single(files.Files);
        Assert.NotNull(files.PrimaryTranscriptFile);
        Assert.Contains("transcript", files.PrimaryTranscriptFile!.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Hello world", content.TranscriptText);
        Assert.Contains("Speaker 1", content.SpeakerFormattedTranscript);
        Assert.Contains("Speaker 1", speakerTranscript);
        Assert.True(speechClient.Cancelled);
        Assert.True(speechClient.Deleted);
        Assert.NotNull(speechClient.LastStartRequest);
        Assert.Equal("Quarterly review", speechClient.LastStartRequest!.DisplayName);
    }

    private sealed class FakeAdviserInfoProvider : IAdviserInfoProvider
    {
        public Task<AdviserInfo?> GetByIdAsync(string adviserId, CancellationToken ct = default)
            => Task.FromResult<AdviserInfo?>(new AdviserInfo
            {
                AdviserId = adviserId,
                DisplayName = "Adviser Example",
                MailboxUserId = "adviser@example.com"
            });
    }

    private sealed class FakeJoinTokenIssuer : IJoinTokenIssuer
    {
        public Task<IssuedJoinToken> IssueForMeetingAsync(MeetingSession session, string displayName, string role, CancellationToken ct = default)
            => Task.FromResult(new IssuedJoinToken
            {
                MeetingId = session.MeetingId,
                GroupId = session.GroupId,
                UserId = "user-123",
                Token = "join-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                DisplayName = displayName
            });

        public Task<IssuedIdentityToken> IssueIdentityTokenAsync(CancellationToken ct = default)
            => Task.FromResult(new IssuedIdentityToken
            {
                IdentityId = "identity-123",
                Token = "identity-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            });
    }

    private sealed class FakeMeetingSessionRepository : IMeetingSessionRepository
    {
        private readonly Dictionary<string, MeetingSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public Task InsertAsync(MeetingSession session, CancellationToken ct = default)
        {
            _sessions[session.MeetingId] = session;
            return Task.CompletedTask;
        }

        public Task<MeetingSession?> GetByIdAsync(string meetingId, CancellationToken ct = default)
            => Task.FromResult(_sessions.TryGetValue(meetingId, out var session) ? session : null);

        public Task<MeetingSession?> GetByGroupIdAsync(string groupId, CancellationToken ct = default)
            => Task.FromResult(_sessions.Values.FirstOrDefault(session => string.Equals(session.GroupId, groupId, StringComparison.OrdinalIgnoreCase)));

        public Task<MeetingSession?> UpdateConsentAsync(string groupId, bool consent, DateTimeOffset consentTimestampUtc, CancellationToken ct = default)
        {
            var session = _sessions.Values.FirstOrDefault(item => string.Equals(item.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
            if (session is null)
            {
                return Task.FromResult<MeetingSession?>(null);
            }

            var updated = new MeetingSession
            {
                MeetingId = session.MeetingId,
                GroupId = session.GroupId,
                AdviserId = session.AdviserId,
                AdviserName = session.AdviserName,
                LeadId = session.LeadId,
                MeetingType = session.MeetingType,
                Title = session.Title,
                StartUtc = session.StartUtc,
                EndUtc = session.EndUtc,
                ClientEmail = session.ClientEmail,
                ClientName = session.ClientName,
                ConsentToRecording = consent,
                ConsentTimestampUtc = consent ? consentTimestampUtc : null,
                Status = session.Status,
                CalendarEventReference = session.CalendarEventReference,
                Attendees = session.Attendees,
                Recordings = session.Recordings,
                Transcription = session.Transcription
            };

            _sessions[updated.MeetingId] = updated;
            return Task.FromResult<MeetingSession?>(updated);
        }
    }

    private sealed class FakeMeetingRecordingRepository : IMeetingRecordingRepository
    {
        private readonly Dictionary<string, MeetingRecordingArtifact> _recordings = new(StringComparer.OrdinalIgnoreCase);

        public Task<MeetingRecordingArtifact> StartAsync(string meetingId, string blobName, string blobUrl, DateTimeOffset startedUtc, CancellationToken ct = default)
        {
            var artifact = new MeetingRecordingArtifact
            {
                RecordingId = "recording-123",
                MeetingId = meetingId,
                BlobName = blobName,
                BlobUrl = blobUrl,
                RecordingStartUtc = startedUtc
            };
            _recordings[artifact.RecordingId] = artifact;
            return Task.FromResult(artifact);
        }

        public Task<MeetingRecordingArtifact?> StopAsync(string recordingId, DateTimeOffset stoppedUtc, CancellationToken ct = default)
        {
            if (!_recordings.TryGetValue(recordingId, out var artifact))
            {
                return Task.FromResult<MeetingRecordingArtifact?>(null);
            }

            var updated = new MeetingRecordingArtifact
            {
                RecordingId = artifact.RecordingId,
                MeetingId = artifact.MeetingId,
                BlobName = artifact.BlobName,
                BlobUrl = artifact.BlobUrl,
                RecordingStartUtc = artifact.RecordingStartUtc,
                RecordingEndUtc = stoppedUtc,
                DurationSeconds = (int)Math.Max(0, Math.Round((stoppedUtc - artifact.RecordingStartUtc).TotalSeconds))
            };
            _recordings[recordingId] = updated;
            return Task.FromResult<MeetingRecordingArtifact?>(updated);
        }

        public Task<IReadOnlyList<MeetingRecordingArtifact>> ListAsync(string? meetingId, CancellationToken ct = default)
        {
            var items = string.IsNullOrWhiteSpace(meetingId)
                ? _recordings.Values.ToArray()
                : _recordings.Values.Where(recording => string.Equals(recording.MeetingId, meetingId, StringComparison.OrdinalIgnoreCase)).ToArray();

            return Task.FromResult<IReadOnlyList<MeetingRecordingArtifact>>(items);
        }

        public Task<MeetingRecordingArtifact?> GetAsync(string recordingId, CancellationToken ct = default)
            => Task.FromResult(_recordings.TryGetValue(recordingId, out var artifact) ? artifact : null);
    }

    private sealed class FakeMeetingTranscriptionRepository : IMeetingTranscriptionRepository
    {
        private readonly Dictionary<string, MeetingTranscriptionArtifact> _byJobId = new(StringComparer.OrdinalIgnoreCase);

        public Task AttachJobAsync(string meetingId, MeetingTranscriptionArtifact transcription, CancellationToken ct = default)
        {
            _byJobId[transcription.TranscriptionId] = transcription;
            return Task.CompletedTask;
        }

        public Task<MeetingTranscriptionArtifact?> GetByTranscriptionIdAsync(string transcriptionId, CancellationToken ct = default)
            => Task.FromResult(_byJobId.TryGetValue(transcriptionId, out var artifact) ? artifact : null);

        public Task AttachContentAsync(string transcriptionId, string fullText, string? summaryText, CancellationToken ct = default)
        {
            if (_byJobId.TryGetValue(transcriptionId, out var artifact))
            {
                _byJobId[transcriptionId] = new MeetingTranscriptionArtifact
                {
                    TranscriptionId = artifact.TranscriptionId,
                    Language = artifact.Language,
                    FullText = fullText,
                    SummaryText = summaryText
                };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSpeechTranscriptionClient : ISpeechTranscriptionClient
    {
        public SpeechTranscriptionStartRequest? LastStartRequest { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Deleted { get; private set; }

        public Task<SpeechTranscriptionJobStatus> StartJobAsync(SpeechTranscriptionStartRequest request, CancellationToken ct = default)
        {
            LastStartRequest = request;
            return Task.FromResult(new SpeechTranscriptionJobStatus
            {
                JobId = "job-123",
                Status = "Running",
                DisplayName = request.DisplayName,
                CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                LastActionDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                Locale = request.Locale,
                Self = new Uri("https://speech.example/transcriptions/job-123")
            });
        }

        public Task<SpeechTranscriptionJobStatus> CheckJobStatusAsync(string jobId, CancellationToken ct = default)
            => Task.FromResult(new SpeechTranscriptionJobStatus
            {
                JobId = jobId,
                Status = "Succeeded",
                DisplayName = "Quarterly review",
                CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                LastActionDateTime = new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.Zero),
                Locale = "en-GB",
                Self = new Uri($"https://speech.example/transcriptions/{jobId}")
            });

        public Task<SpeechTranscriptionFilesResult> GetJobFilesAsync(string jobId, CancellationToken ct = default)
        {
            var transcript = new SpeechTranscriptionFile
            {
                Name = "transcript.vtt",
                Kind = "Transcription",
                CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 31, 0, TimeSpan.Zero),
                SizeInBytes = 1024,
                ContentLength = 1024,
                Self = new Uri($"https://speech.example/transcriptions/{jobId}/files/transcript.vtt"),
                ContentUri = new Uri("https://speech.example/files/transcript.vtt")
            };

            return Task.FromResult(new SpeechTranscriptionFilesResult
            {
                Files = [transcript],
                PrimaryTranscriptFile = transcript
            });
        }

        public Task<SpeechTranscriptContent> GetTranscriptByJobAsync(string jobId, CancellationToken ct = default)
            => Task.FromResult(new SpeechTranscriptContent
            {
                TranscriptText = "Hello world",
                SpeakerFormattedTranscript = "Speaker 1: Hello world"
            });

        public Task CancelJobAsync(string jobId, CancellationToken ct = default)
        {
            Cancelled = true;
            return Task.CompletedTask;
        }

        public Task DeleteJobAsync(string jobId, CancellationToken ct = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }
    }
}
