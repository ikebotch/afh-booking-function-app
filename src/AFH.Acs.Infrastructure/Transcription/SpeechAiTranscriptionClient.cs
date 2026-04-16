using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Common.SpeechAI.Abstractions;
using AFH.Common.SpeechAI.Extensions;
using AFH.Common.SpeechAI.Models;
using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models.Responses;

namespace AFH.Acs.Infrastructure.Transcription;

public sealed class SpeechAiTranscriptionClient(ISpeechAiService speechAi) : ISpeechTranscriptionClient
{
    public async Task<SpeechTranscriptionJobStatus> StartJobAsync(SpeechTranscriptionStartRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await speechAi.StartJobAsync(new StartTranscriptionRequest
        {
            ContentUrls = request.ContentUrls.ToList(),
            DisplayName = request.DisplayName,
            Locale = request.Locale,
            Properties = new TranscriptionJobProperties
            {
                DiarizationEnabled = request.Settings?.DiarizationEnabled,
                WordLevelTimestampsEnabled = request.Settings?.WordLevelTimestampsEnabled
            }
        }, ct);

        return MapJobStatus(response);
    }

    public async Task<SpeechTranscriptionJobStatus> CheckJobStatusAsync(string jobId, CancellationToken ct = default)
        => MapJobStatus(await speechAi.CheckJobStatusAsync(jobId, ct));

    public async Task<SpeechTranscriptionFilesResult> GetJobFilesAsync(string jobId, CancellationToken ct = default)
    {
        var response = await speechAi.GetJobFilesAsync(jobId, ct);
        return new SpeechTranscriptionFilesResult
        {
            Files = response.Files.Select(MapFile).ToArray(),
            PrimaryTranscriptFile = response.GetPrimaryTranscriptFile() is { } primary ? MapFile(primary) : null
        };
    }

    public async Task<SpeechTranscriptContent> GetTranscriptByJobAsync(string jobId, CancellationToken ct = default)
    {
        var response = await speechAi.GetTranscriptByJobAsync(jobId, ct);
        return new SpeechTranscriptContent
        {
            TranscriptText = response.ToTranscriptText(),
            SpeakerFormattedTranscript = response.ToSpeakerFormattedTranscript()
        };
    }

    public Task CancelJobAsync(string jobId, CancellationToken ct = default)
        => speechAi.CancelJobAsync(jobId, ct);

    public Task DeleteJobAsync(string jobId, CancellationToken ct = default)
        => speechAi.DeleteJobAsync(jobId, ct);

    private static SpeechTranscriptionJobStatus MapJobStatus(JobStatusResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new SpeechTranscriptionJobStatus
        {
            JobId = ExtractJobId(response),
            Status = response.Status ?? string.Empty,
            DisplayName = response.DisplayName,
            CreatedDateTime = response.CreatedDateTime,
            LastActionDateTime = response.LastActionDateTime,
            Locale = response.Locale,
            Model = response.Model,
            Self = response.Self ?? response.Links?.Self
        };
    }

    private static SpeechTranscriptionFile MapFile(JobFileItem file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new SpeechTranscriptionFile
        {
            Name = file.Name ?? string.Empty,
            Kind = file.Kind,
            CreatedDateTime = file.CreatedDateTime,
            SizeInBytes = file.SizeInBytes,
            ContentLength = file.ContentLength,
            Self = file.Self,
            ContentUrl = file.Links?.ContentUrl,
            ContentUri = file.Links?.ContentUri
        };
    }

    private static string ExtractJobId(JobStatusResponse response)
        => ExtractJobId(response.Self) ?? ExtractJobId(response.Links?.Self) ?? string.Empty;

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
