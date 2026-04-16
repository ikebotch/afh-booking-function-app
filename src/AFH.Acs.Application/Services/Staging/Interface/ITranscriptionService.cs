using AFH.Acs.Recorder.DTOs;

namespace AFH.Acs.Recorder.Services.Interface;

public interface ITranscriptionService
{

    Task<TranscriptionRunResult> TranscribeRecordingAsync(
          RecordingTranscriptionRequest request,
          CancellationToken ct = default);
    Task<IReadOnlyList<RecordingTranscriptionRequest>> GetTranscriptionDataAsync(
        CancellationToken ct = default);
}