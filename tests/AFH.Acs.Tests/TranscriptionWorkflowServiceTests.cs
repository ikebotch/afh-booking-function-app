using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Function.Services.Meetings;
using AFH.Acs.Function.Services.Recordings;
using AFH.Acs.Function.Services.Transcription;
using AFH.Common.SpeechAI.Abstractions;
using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models;
using AFH.Common.SpeechAI.Models.Responses;
using Microsoft.Extensions.Configuration;

namespace AFH.Acs.Tests;

public sealed class TranscriptionWorkflowServiceTests
{
    [Fact]
    public async Task Workflow_Covers_Meeting_Join_Link_Recording_And_Transcription()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:JoinBaseUrl"] = "https://meetings.example"
            })
            .Build();

        var store = new MeetingWorkflowStore(configuration);
        var recordings = new MetadataMeetingRecordingService(store);
        var speechAi = new FakeSpeechAiService();
        var service = new TranscriptionWorkflowService(speechAi, store);

        var meeting = await store.CreateMeetingAsync(new ScheduleMeetingRequest
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

        var identity = await store.IssueIdentityTokenAsync();
        var joinToken = await store.IssueJoinTokenAsync(meeting.GroupId, new JoinTokenRequest
        {
            DisplayName = "Client Example",
            Role = "Client"
        });
        var link = await store.CreateMeetingLinkAsync(new CreateMeetingLinkRequest
        {
            BookingId = "booking-123"
        });

        var startedRecording = await recordings.StartRecordingAsync(new StartRecordingRequest
        {
            MeetingId = meeting.MeetingId
        });

        var stoppedRecording = await recordings.StopRecordingAsync(new StopRecordingRequest
        {
            RecordingId = startedRecording.RecordingId
        });

        var submitted = await service.SubmitAsync(
            meeting.MeetingId,
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

        Assert.Equal(meeting.MeetingId, submitted.MeetingId);
        Assert.Equal("job-123", submitted.JobId);
        Assert.Equal("Running", submitted.Status);
        Assert.Equal("https://storage.example/recordings/meeting-42.wav", submitted.SourceUrl);
        Assert.False(string.IsNullOrWhiteSpace(identity.IdentityId));
        Assert.False(string.IsNullOrWhiteSpace(identity.Token));
        Assert.Equal(meeting.MeetingId, joinToken.MeetingId);
        Assert.Equal(meeting.GroupId, joinToken.GroupId);
        Assert.Equal("booking-123", link.BookingId);
        Assert.Contains("https://meetings.example/meeting/", link.JoinUrl);
        Assert.Equal(meeting.MeetingId, startedRecording.MeetingId);
        Assert.NotNull(stoppedRecording.RecordingEndUtc);

        var status = await service.GetStatusAsync(submitted.JobId);
        Assert.Equal("Succeeded", status.Status);
        Assert.Equal("job-123", status.JobId);

        var files = await service.GetFilesAsync(submitted.JobId);
        Assert.Equal("job-123", files.JobId);
        Assert.Single(files.Files);
        Assert.NotNull(files.PrimaryTranscriptFile);
        Assert.Contains("transcript", files.PrimaryTranscriptFile!.Name, StringComparison.OrdinalIgnoreCase);

        var content = await service.GetContentAsync(submitted.JobId);
        Assert.Equal("job-123", content.JobId);
        Assert.Equal("transcript.vtt", content.TranscriptFileName);
        Assert.Equal("Hello world", content.TranscriptText);
        Assert.Contains("Speaker 1", content.SpeakerFormattedTranscript);

        var speakerTranscript = await service.GetSpeakerFormattedTranscriptAsync(submitted.JobId);
        Assert.Contains("Speaker 1", speakerTranscript);

        await service.CancelAsync(submitted.JobId);
        await service.DeleteAsync(submitted.JobId);

        Assert.True(speechAi.Cancelled);
        Assert.True(speechAi.Deleted);
        Assert.NotNull(speechAi.LastStartRequest);
        Assert.Equal("Quarterly review", speechAi.LastStartRequest!.DisplayName);

        var storedMeeting = await store.GetMeetingByIdAsync(meeting.MeetingId);
        Assert.NotNull(storedMeeting);
        Assert.NotNull(storedMeeting!.Transcription);
        Assert.Equal("job-123", storedMeeting.Transcription!.TranscriptionId);
        Assert.Equal("Hello world", storedMeeting.Transcription.FullText);
        Assert.Contains("Speaker 1", storedMeeting.Transcription.SummaryText);
        Assert.Single(storedMeeting.Recordings);
        Assert.Equal(startedRecording.RecordingId, storedMeeting.Recordings[0].RecordingId);
    }

    private sealed class FakeSpeechAiService : ISpeechAiService
    {
        public StartTranscriptionRequest? LastStartRequest { get; private set; }

        public bool Cancelled { get; private set; }

        public bool Deleted { get; private set; }

        public Task<JobStatusResponse> StartJobAsync(string fileUrl, CancellationToken cancellationToken = default)
            => StartJobAsync(new Uri(fileUrl), cancellationToken);

        public Task<JobStatusResponse> StartJobAsync(Uri fileUrl, CancellationToken cancellationToken = default)
            => StartJobAsync(new StartTranscriptionRequest { ContentUrls = [fileUrl] }, cancellationToken);

        public Task<JobStatusResponse> StartJobAsync(StartTranscriptionRequest request, CancellationToken cancellationToken = default)
        {
            LastStartRequest = request;
            return Task.FromResult(new JobStatusResponse
            {
                Status = "Running",
                DisplayName = request.DisplayName,
                CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                LastActionDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                Locale = request.Locale,
                Self = new Uri("https://speech.example/transcriptions/job-123")
            });
        }

        public Task<JobStatusResponse> CheckJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new JobStatusResponse
            {
                Status = "Succeeded",
                DisplayName = "Quarterly review",
                CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                LastActionDateTime = new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.Zero),
                Locale = "en-GB",
                Self = new Uri($"https://speech.example/transcriptions/{jobId}")
            });

        public Task<JobStatusResponse> WaitForCompletionAsync(string jobId, TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => CheckJobStatusAsync(jobId, cancellationToken);

        public Task<JobFilesResponse> GetJobFilesAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new JobFilesResponse
            {
                Self = new Uri($"https://speech.example/transcriptions/{jobId}/files"),
                Files =
                [
                    new JobFileItem
                    {
                        Name = "transcript.vtt",
                        Kind = "Transcription",
                        CreatedDateTime = new DateTimeOffset(2026, 4, 1, 9, 31, 0, TimeSpan.Zero),
                        SizeInBytes = 1024,
                        ContentLength = 1024,
                        Self = new Uri($"https://speech.example/transcriptions/{jobId}/files/transcript.vtt"),
                        Links = new ResourceLinks
                        {
                            ContentUri = new Uri("https://speech.example/files/transcript.vtt")
                        }
                    }
                ]
            });

        public Task<JobFileItem> GetPrimaryTranscriptFileAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(new JobFileItem
            {
                Name = "transcript.vtt",
                Kind = "Transcription",
                Self = new Uri($"https://speech.example/transcriptions/{jobId}/files/transcript.vtt"),
                Links = new ResourceLinks
                {
                    ContentUri = new Uri("https://speech.example/files/transcript.vtt")
                }
            });

        public Task<TranscriptFileResponse> GetTranscriptAsync(string fileUrl, CancellationToken cancellationToken = default)
            => GetTranscriptAsync(new Uri(fileUrl), cancellationToken);

        public Task<TranscriptFileResponse> GetTranscriptAsync(Uri fileUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateTranscript(fileUrl));

        public Task<TranscriptFileResponse> GetTranscriptByJobAsync(string jobId, CancellationToken cancellationToken = default)
            => Task.FromResult(CreateTranscript(new Uri($"https://speech.example/transcriptions/{jobId}/files/transcript.vtt")));

        public async Task<string> GetTranscriptTextAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var transcript = await GetTranscriptByJobAsync(jobId, cancellationToken);
            return transcript.CombinedRecognizedPhrases.First().Display!;
        }

        public Task<string> GetTranscriptTextAsync(TranscriptFileResponse transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(transcript.CombinedRecognizedPhrases.First().Display!);

        public async Task<string> GetSpeakerFormattedTranscriptAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var transcript = await GetTranscriptByJobAsync(jobId, cancellationToken);
            var phrase = transcript.RecognizedPhrases.First();
            return $"{phrase.Speaker}: {phrase.NBest.First().Display}";
        }

        public Task<string> GetSpeakerFormattedTranscriptAsync(TranscriptFileResponse transcript, CancellationToken cancellationToken = default)
            => Task.FromResult("Speaker 1: Hello world");

        public async Task<TranscriptFileResponse> TranscribeAndWaitAsync(string fileUrl, TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => await GetTranscriptByJobAsync("job-123", cancellationToken);

        public async Task<TranscriptFileResponse> TranscribeAndWaitAsync(Uri fileUrl, TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => await GetTranscriptByJobAsync("job-123", cancellationToken);

        public async Task<TranscriptFileResponse> TranscribeAndWaitAsync(StartTranscriptionRequest request, TimeSpan? pollInterval = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => await GetTranscriptByJobAsync("job-123", cancellationToken);

        public Task CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            Cancelled = true;
            return Task.CompletedTask;
        }

        public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }

        private static TranscriptFileResponse CreateTranscript(Uri source)
            => new()
            {
                Source = source.ToString(),
                Timestamp = new DateTimeOffset(2026, 4, 1, 9, 31, 0, TimeSpan.Zero),
                DurationInTicks = TimeSpan.FromMinutes(42).Ticks,
                CombinedRecognizedPhrases =
                [
                    new CombinedRecognizedPhrase
                    {
                        Display = "Hello world"
                    }
                ],
                RecognizedPhrases =
                [
                    new RecognizedPhrase
                    {
                        OffsetInTicks = 0,
                        DurationInTicks = TimeSpan.FromSeconds(2).Ticks,
                        Speaker = "1",
                        Channel = 0,
                        Locale = "en-GB",
                        NBest =
                        [
                            new PhraseAlternative
                            {
                                Display = "Hello world",
                                Lexical = "hello world"
                            }
                        ]
                    }
                ]
            };
    }
}
