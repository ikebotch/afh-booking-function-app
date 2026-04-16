using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Domain.Entities;
using AFH.Common.SpeechAI.Extensions;
using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models.Responses;

namespace AFH.Acs.Application.Services.Transcription;

public sealed class TranscriptionWorkflowService(
    ISpeechTranscriptionClient speechClient,
    IMeetingTranscriptionRepository repository,
    IMeetingSessionRepository sessions) : ITranscriptionWorkflowService
{
    public async Task<TranscriptionJobResponse> SubmitAsync(string? meetingId, SubmitTranscriptionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contentUrl = RequireAbsoluteUri(request.ContentUrl, nameof(request.ContentUrl));
        var sdkResponse = await speechClient.StartJobAsync(new StartTranscriptionRequest
        {
            ContentUrls = [contentUrl],
            DisplayName = request.DisplayName,
            Locale = request.Locale,
            Properties = new TranscriptionJobProperties
            {
                DiarizationEnabled = request.Settings?.DiarizationEnabled,
                WordLevelTimestampsEnabled = request.Settings?.WordLevelTimestampsEnabled
            }
        }, ct);

        var result = MapJobResponse(sdkResponse, meetingId, contentUrl);

        if (!string.IsNullOrWhiteSpace(meetingId))
        {
            var session = await sessions.GetByIdAsync(meetingId.Trim(), ct);
            if (session is not null)
            {
                await repository.AttachJobAsync(session.MeetingId, new MeetingTranscriptionArtifact
                {
                    TranscriptionId = result.JobId,
                    Language = result.Locale ?? "en-GB",
                    FullText = string.Empty,
                    SummaryText = null
                }, ct);
            }
        }

        return result;
    }

    public async Task<TranscriptionJobResponse> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        var sdkResponse = await speechClient.CheckJobStatusAsync(RequireJobId(jobId), ct);
        return MapJobResponse(sdkResponse, null, null, jobId);
    }

    public async Task<TranscriptionFilesResponse> GetFilesAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var sdkResponse = await speechClient.GetJobFilesAsync(resolvedJobId, ct);
        var files = sdkResponse.Files
            .Select(MapFileResponse)
            .ToArray();

        var primary = sdkResponse.GetPrimaryTranscriptFile();
        return new TranscriptionFilesResponse
        {
            JobId = resolvedJobId,
            PrimaryTranscriptFile = primary is null ? null : MapFileResponse(primary),
            Files = files
        };
    }

    public async Task<TranscriptionContentResponse> GetContentAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechClient.GetTranscriptByJobAsync(resolvedJobId, ct);
        var files = await speechClient.GetJobFilesAsync(resolvedJobId, ct);
        var primary = files.GetPrimaryTranscriptFile();
        var transcriptText = transcript.ToTranscriptText();
        var speakerFormattedTranscript = transcript.ToSpeakerFormattedTranscript();

        await repository.AttachContentAsync(resolvedJobId, transcriptText, speakerFormattedTranscript, ct);

        return new TranscriptionContentResponse
        {
            JobId = resolvedJobId,
            TranscriptFileName = primary?.Name,
            TranscriptFileUrl = primary?.GetContentUri()?.ToString(),
            TranscriptText = transcriptText,
            SpeakerFormattedTranscript = speakerFormattedTranscript
        };
    }

    public async Task<string> GetSpeakerFormattedTranscriptAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechClient.GetTranscriptByJobAsync(resolvedJobId, ct);
        return transcript.ToSpeakerFormattedTranscript();
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
        => speechClient.CancelJobAsync(RequireJobId(jobId), ct);

    public Task DeleteAsync(string jobId, CancellationToken ct = default)
        => speechClient.DeleteJobAsync(RequireJobId(jobId), ct);

    private static TranscriptionJobResponse MapJobResponse(JobStatusResponse response, string? meetingId, Uri? sourceUrl, string? jobIdOverride = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        var jobId = jobIdOverride
            ?? ExtractJobId(response)
            ?? string.Empty;

        return new TranscriptionJobResponse
        {
            MeetingId = meetingId,
            JobId = jobId,
            Status = response.Status,
            DisplayName = response.DisplayName,
            CreatedDateTime = response.CreatedDateTime,
            LastActionDateTime = response.LastActionDateTime,
            Locale = response.Locale,
            Model = response.Model,
            SourceUrl = sourceUrl?.ToString()
        };
    }

    private static TranscriptionFileResponse MapFileResponse(JobFileItem file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new TranscriptionFileResponse
        {
            Name = file.Name ?? string.Empty,
            Kind = file.Kind,
            CreatedDateTime = file.CreatedDateTime,
            SizeInBytes = file.SizeInBytes,
            ContentLength = file.ContentLength,
            Self = file.Self?.ToString(),
            ContentUrl = file.Links?.ContentUrl?.ToString(),
            ContentUri = file.Links?.ContentUri?.ToString()
        };
    }

    private static string RequireJobId(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("jobId is required.", nameof(jobId));
        }

        return jobId.Trim();
    }

    private static Uri RequireAbsoluteUri(string url, string paramName)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("A valid absolute content URL is required.", paramName);
        }

        return uri;
    }

    private static string? ExtractJobId(JobStatusResponse response)
    {
        return ExtractJobId(response.Self) ?? ExtractJobId(response.Links?.Self);
    }

    private static string? ExtractJobId(Uri? jobUri)
    {
        if (jobUri is null)
        {
            return null;
        }

        var trimmed = jobUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return trimmed.Length == 0 ? null : trimmed[^1];
    }
}
