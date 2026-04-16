using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Recordings;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Domain.Entities;
using AFH.Acs.Infrastructure.Options;
using AFH.Acs.Infrastructure.Recordings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Tests;

public sealed class RecordingServiceRegistrationTests
{
    [Fact]
    public void RecordingServices_Default_To_Metadata()
    {
        var provider = BuildProvider();

        var service = provider.GetRequiredService<IMeetingRecordingService>();

        Assert.IsType<MetadataMeetingRecordingService>(service);
    }

    [Fact]
    public void RecordingServices_Select_Metadata_When_Configured()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{RecordingOptions.SectionName}:Mode"] = nameof(RecordingMode.Metadata)
        });

        var service = provider.GetRequiredService<IMeetingRecordingService>();

        Assert.IsType<MetadataMeetingRecordingService>(service);
    }

    [Fact]
    public void RecordingServices_Select_LiveAcs_When_Configured()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{RecordingOptions.SectionName}:Mode"] = nameof(RecordingMode.LiveAcs)
        });

        var service = provider.GetRequiredService<IMeetingRecordingService>();

        Assert.IsType<LiveAcsMeetingRecordingService>(service);
    }

    [Fact]
    public async Task MetadataRecordingService_Works_Through_The_Abstraction()
    {
        var provider = BuildProvider();
        var recordings = provider.GetRequiredService<IMeetingRecordingService>();
        var sessions = provider.GetRequiredService<IMeetingSessionRepository>() as FakeMeetingSessionRepository
            ?? throw new InvalidOperationException("Expected fake meeting session repository.");

        sessions.Seed(new MeetingSession
        {
            MeetingId = "meeting-123",
            GroupId = "group-456",
            AdviserId = "adv-123",
            LeadId = "lead-456",
            MeetingType = "Review",
            Title = "Quarterly review",
            StartUtc = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            ClientEmail = "client@example.com"
        });

        var started = await recordings.StartRecordingAsync(new StartRecordingRequest
        {
            MeetingId = "meeting-123"
        });

        var stopped = await recordings.StopRecordingAsync(new StopRecordingRequest
        {
            RecordingId = started.RecordingId
        });

        Assert.Equal("meeting-123", started.MeetingId);
        Assert.Equal("group-456", started.GroupId);
        Assert.NotNull(stopped.RecordingEndUtc);
        Assert.Equal(started.RecordingId, stopped.RecordingId);
        Assert.Single(await recordings.ListRecordingsAsync("meeting-123"));
        Assert.NotNull(await recordings.GetRecordingAsync(started.RecordingId));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRecordingModule(configuration);
        services.AddSingleton<IMeetingSessionRepository, FakeMeetingSessionRepository>();
        services.AddSingleton<IMeetingRecordingRepository, FakeMeetingRecordingRepository>();

        return services.BuildServiceProvider();
    }

    private sealed class FakeMeetingSessionRepository : IMeetingSessionRepository
    {
        private readonly Dictionary<string, MeetingSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(MeetingSession session)
            => _sessions[session.MeetingId] = session;

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
                RecordingId = Guid.NewGuid().ToString("N"),
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
}
