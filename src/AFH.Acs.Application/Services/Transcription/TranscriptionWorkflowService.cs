using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;
using AFH.Acs.Domain.Entities;

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
        var transcriptionResponse = await speechClient.StartJobAsync(new SpeechTranscriptionStartRequest
        {
            ContentUrls = [contentUrl],
            DisplayName = request.DisplayName,
            Locale = request.Locale,
            Settings = new SpeechTranscriptionSettings
            {
                DiarizationEnabled = request.Settings?.DiarizationEnabled,
                WordLevelTimestampsEnabled = request.Settings?.WordLevelTimestampsEnabled
            }
        }, ct);

        var result = MapJobResponse(transcriptionResponse, meetingId, contentUrl);

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
        var transcriptionResponse = await speechClient.CheckJobStatusAsync(RequireJobId(jobId), ct);
        return MapJobResponse(transcriptionResponse, null, null);
    }

    public async Task<TranscriptionFilesResponse> GetFilesAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var fileResult = await speechClient.GetJobFilesAsync(resolvedJobId, ct);
        var files = fileResult.Files
            .Select(MapFileResponse)
            .ToArray();

        return new TranscriptionFilesResponse
        {
            JobId = resolvedJobId,
            PrimaryTranscriptFile = fileResult.PrimaryTranscriptFile is null ? null : MapFileResponse(fileResult.PrimaryTranscriptFile),
            Files = files
        };
    }

    public async Task<TranscriptionContentResponse> GetContentAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechClient.GetTranscriptByJobAsync(resolvedJobId, ct);
        var files = await speechClient.GetJobFilesAsync(resolvedJobId, ct);
        var primary = files.PrimaryTranscriptFile;

        await repository.AttachContentAsync(resolvedJobId, transcript.TranscriptText, transcript.SpeakerFormattedTranscript, ct);

        return new TranscriptionContentResponse
        {
            JobId = resolvedJobId,
            TranscriptFileName = primary?.Name,
            TranscriptFileUrl = primary?.ContentUri?.ToString(),
            TranscriptText = transcript.TranscriptText,
            SpeakerFormattedTranscript = transcript.SpeakerFormattedTranscript
        };
    }

    public async Task<string> GetSpeakerFormattedTranscriptAsync(string jobId, CancellationToken ct = default)
    {
        var resolvedJobId = RequireJobId(jobId);
        var transcript = await speechClient.GetTranscriptByJobAsync(resolvedJobId, ct);
        return transcript.SpeakerFormattedTranscript;
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
        => speechClient.CancelJobAsync(RequireJobId(jobId), ct);

    public Task DeleteAsync(string jobId, CancellationToken ct = default)
        => speechClient.DeleteJobAsync(RequireJobId(jobId), ct);

    private static TranscriptionJobResponse MapJobResponse(SpeechTranscriptionJobStatus response, string? meetingId, Uri? sourceUrl)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new TranscriptionJobResponse
        {
            MeetingId = meetingId,
            JobId = response.JobId,
            Status = response.Status,
            DisplayName = response.DisplayName,
            CreatedDateTime = response.CreatedDateTime,
            LastActionDateTime = response.LastActionDateTime,
            Locale = response.Locale,
            Model = response.Model,
            SourceUrl = sourceUrl?.ToString()
        };
    }

    private static TranscriptionFileResponse MapFileResponse(SpeechTranscriptionFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new TranscriptionFileResponse
        {
            Name = file.Name,
            Kind = file.Kind,
            CreatedDateTime = file.CreatedDateTime,
            SizeInBytes = file.SizeInBytes,
            ContentLength = file.ContentLength,
            Self = file.Self?.ToString(),
            ContentUrl = file.ContentUrl?.ToString(),
            ContentUri = file.ContentUri?.ToString()
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
}
