using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Function.Options;
using AFH.Acs.Function.Services.Meetings;
using AFH.Acs.Function.Services.Recordings;
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
        var store = provider.GetRequiredService<IMeetingWorkflowStore>();

        var meeting = await store.CreateMeetingAsync(new ScheduleMeetingRequest
        {
            AdviserId = "adv-123",
            LeadId = "lead-456",
            MeetingType = "Review",
            Title = "Quarterly review",
            Start = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            ClientEmail = "client@example.com"
        });

        var started = await recordings.StartRecordingAsync(new StartRecordingRequest
        {
            MeetingId = meeting.MeetingId
        });

        var stopped = await recordings.StopRecordingAsync(new StopRecordingRequest
        {
            RecordingId = started.RecordingId
        });

        Assert.Equal(meeting.MeetingId, started.MeetingId);
        Assert.NotNull(stopped.RecordingEndUtc);
        Assert.Equal(started.RecordingId, stopped.RecordingId);
        Assert.Single(await recordings.ListRecordingsAsync(meeting.MeetingId));
        Assert.NotNull(await recordings.GetRecordingAsync(started.RecordingId));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IMeetingWorkflowStore, MeetingWorkflowStore>();
        services.AddRecordingServices(configuration);

        return services.BuildServiceProvider();
    }
}
