using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Function.Services.Meetings;
using AFH.Common.SpeechAI.Abstractions;
using AFH.Common.SpeechAI.Extensions;
using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models.Responses;

namespace AFH.Acs.Function.Services.Transcription;

public sealed class TranscriptionWorkflowService(
    ISpeechAiService speechAi,
    IMeetingWorkflowStore meetings) : ITranscriptionWorkflowService
{
    public async Task<TranscriptionJobResponse> SubmitAsync(string? meetingId, SubmitTranscriptionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contentUrl = RequireAbsoluteUri(request.ContentUrl, nameof(request.ContentUrl));
        var sdkResponse = await speechAi.StartJobAsync(new StartTranscriptionRequest
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
        await meetings.AttachTranscriptionAsync(meetingId, result, ct);
        return result;
    }

    public async Task<TranscriptionJobResponse> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        var sdkResponse = await speechAi.CheckJobStatusAsync(RequireJobId(jobId), ct);
        return MapJobResponse(sdkResponse, null, null, jobId);
    }

    public async Task<TranscriptionFilesResponse> GetFilesAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var sdkResponse = await speechAi.GetJobFilesAsync(resolvedJobId, ct);
        var files = sdkResponse.Files
            .Select(MapFileResponse)
            .ToArray();

        var primary = sdkResponse.GetPrimaryTranscriptFile();
        return new TranscriptionFilesResponse
        {
            JobId = resolvedJobId,
            PrimaryTranscriptFile = primary is null ? null : MapFileResponse(primary)
            ,
            Files = files
        };
    }

    public async Task<TranscriptionContentResponse> GetContentAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechAi.GetTranscriptByJobAsync(resolvedJobId, ct);
        var files = await speechAi.GetJobFilesAsync(resolvedJobId, ct);
        var primary = files.GetPrimaryTranscriptFile();
        var fileUrl = primary?.GetContentUri()?.ToString();
        var transcriptText = transcript.ToTranscriptText();
        var speakerFormattedTranscript = transcript.ToSpeakerFormattedTranscript();

        await meetings.AttachTranscriptContentAsync(
            resolvedJobId,
            transcriptText,
            speakerFormattedTranscript,
            primary?.Name,
            fileUrl,
            ct);

        return new TranscriptionContentResponse
        {
            JobId = resolvedJobId,
            TranscriptFileName = primary?.Name,
            TranscriptFileUrl = fileUrl,
            TranscriptText = transcriptText,
            SpeakerFormattedTranscript = speakerFormattedTranscript
        };
    }

    public async Task<string> GetSpeakerFormattedTranscriptAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechAi.GetTranscriptByJobAsync(resolvedJobId, ct);
        return transcript.ToSpeakerFormattedTranscript();
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
        => speechAi.CancelJobAsync(RequireJobId(jobId), ct);

    public Task DeleteAsync(string jobId, CancellationToken ct = default)
        => speechAi.DeleteJobAsync(RequireJobId(jobId), ct);

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
