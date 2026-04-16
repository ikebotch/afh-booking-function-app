namespace AFH.Acs.Application.Abstractions.Transcription;

public interface ISpeechTranscriptionClient
{
    Task<SpeechTranscriptionJobStatus> StartJobAsync(SpeechTranscriptionStartRequest request, CancellationToken ct = default);
    Task<SpeechTranscriptionJobStatus> CheckJobStatusAsync(string jobId, CancellationToken ct = default);
    Task<SpeechTranscriptionFilesResult> GetJobFilesAsync(string jobId, CancellationToken ct = default);
    Task<SpeechTranscriptContent> GetTranscriptByJobAsync(string jobId, CancellationToken ct = default);
    Task CancelJobAsync(string jobId, CancellationToken ct = default);
    Task DeleteJobAsync(string jobId, CancellationToken ct = default);
}
