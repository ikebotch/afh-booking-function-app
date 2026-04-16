using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;

namespace AFH.Acs.Application.Abstractions.Transcription;

public interface ITranscriptionWorkflowService
{
    Task<TranscriptionJobResponse> SubmitAsync(string? meetingId, SubmitTranscriptionRequest request, CancellationToken ct = default);
    Task<TranscriptionJobResponse> GetStatusAsync(string jobId, CancellationToken ct = default);
    Task<TranscriptionFilesResponse> GetFilesAsync(string jobId, CancellationToken ct = default);
    Task<TranscriptionContentResponse> GetContentAsync(string jobId, CancellationToken ct = default);
    Task<string> GetSpeakerFormattedTranscriptAsync(string jobId, CancellationToken ct = default);
    Task CancelAsync(string jobId, CancellationToken ct = default);
    Task DeleteAsync(string jobId, CancellationToken ct = default);
}
