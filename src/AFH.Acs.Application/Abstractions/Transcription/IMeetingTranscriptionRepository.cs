using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Transcription;

public interface IMeetingTranscriptionRepository
{
    Task AttachJobAsync(string meetingId, MeetingTranscriptionArtifact transcription, CancellationToken ct = default);
    Task<MeetingTranscriptionArtifact?> GetByTranscriptionIdAsync(string transcriptionId, CancellationToken ct = default);
    Task AttachContentAsync(string transcriptionId, string fullText, string? summaryText, CancellationToken ct = default);
}
