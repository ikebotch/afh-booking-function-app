using AFH.Common.SpeechAI.Models.Requests;
using AFH.Common.SpeechAI.Models.Responses;
using AFH.Common.SpeechAI.Models;

namespace AFH.Acs.Application.Abstractions.Transcription;

public interface ISpeechTranscriptionClient
{
    Task<JobStatusResponse> StartJobAsync(StartTranscriptionRequest request, CancellationToken ct = default);
    Task<JobStatusResponse> CheckJobStatusAsync(string jobId, CancellationToken ct = default);
    Task<JobFilesResponse> GetJobFilesAsync(string jobId, CancellationToken ct = default);
    Task<TranscriptFileResponse> GetTranscriptByJobAsync(string jobId, CancellationToken ct = default);
    Task CancelJobAsync(string jobId, CancellationToken ct = default);
    Task DeleteJobAsync(string jobId, CancellationToken ct = default);
}
