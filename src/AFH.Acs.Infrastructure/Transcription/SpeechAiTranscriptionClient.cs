using AFH.Acs.Application.Abstractions.Transcription;
using AFH.Common.SpeechAI.Abstractions;
using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models.Responses;
using AFH.Common.SpeechAI.Models;

namespace AFH.Acs.Infrastructure.Transcription;

public sealed class SpeechAiTranscriptionClient(ISpeechAiService speechAi) : ISpeechTranscriptionClient
{
    public Task<JobStatusResponse> StartJobAsync(StartTranscriptionRequest request, CancellationToken ct = default)
        => speechAi.StartJobAsync(request, ct);

    public Task<JobStatusResponse> CheckJobStatusAsync(string jobId, CancellationToken ct = default)
        => speechAi.CheckJobStatusAsync(jobId, ct);

    public Task<JobFilesResponse> GetJobFilesAsync(string jobId, CancellationToken ct = default)
        => speechAi.GetJobFilesAsync(jobId, ct);

    public Task<TranscriptFileResponse> GetTranscriptByJobAsync(string jobId, CancellationToken ct = default)
        => speechAi.GetTranscriptByJobAsync(jobId, ct);

    public Task CancelJobAsync(string jobId, CancellationToken ct = default)
        => speechAi.CancelJobAsync(jobId, ct);

    public Task DeleteJobAsync(string jobId, CancellationToken ct = default)
        => speechAi.DeleteJobAsync(jobId, ct);
}
