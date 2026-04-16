using AFH.Acs.Function.Functions.V1.System;

namespace AFH.Acs.Tests;

public sealed class OpenApiDocumentFactoryTests
{
    [Fact]
    public void OpenApiDocument_OnlyIncludesRetainedMeetingAndTranscriptionSurface()
    {
        var json = OpenApiDocumentFactory.CreateJson();

        Assert.Contains("/v1/meet/create", json);
        Assert.Contains("/v1/meet/identity-token", json);
        Assert.Contains("/v1/meet/link", json);
        Assert.Contains("/v1/meet/{groupId}/join-token", json);
        Assert.Contains("/v1/recordings/start", json);
        Assert.Contains("/v1/recordings/{recordingId}", json);
        Assert.Contains("/v1/meetings/{meetingId}/transcriptions", json);
        Assert.Contains("/v1/transcriptions/{jobId}/content", json);
        Assert.Contains("/v1/transcriptions/{jobId}/speaker-content", json);
        Assert.Contains("Meeting orchestration, media, and transcription.", json);
        Assert.DoesNotContain("/v1/leads", json);
        Assert.DoesNotContain("/v1/advisers", json);
        Assert.DoesNotContain("/v1/calendar", json);
        Assert.DoesNotContain("/v1/workspace", json);
    }
}
