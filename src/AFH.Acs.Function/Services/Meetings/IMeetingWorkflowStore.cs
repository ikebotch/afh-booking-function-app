using AFH.Acs.Contract.V1.Requests;
using AFH.Acs.Contract.V1.Responses;

namespace AFH.Acs.Function.Services.Meetings;

public interface IMeetingWorkflowStore
{
    Task<MeetingScheduleResponse> CreateMeetingAsync(ScheduleMeetingRequest request, CancellationToken ct = default);
    Task<MeetingDetailsResponse?> GetMeetingByIdAsync(string meetingId, CancellationToken ct = default);
    Task<MeetingDetailsResponse?> GetMeetingByGroupIdAsync(string groupId, CancellationToken ct = default);
    Task<MeetingConsentResponse> RecordConsentAsync(string groupId, bool consent, CancellationToken ct = default);
    Task<JoinTokenResponse> IssueJoinTokenAsync(string groupId, JoinTokenRequest request, CancellationToken ct = default);
    Task<IdentityTokenResponse> IssueIdentityTokenAsync(CancellationToken ct = default);
    Task<MeetingLinkResponse> CreateMeetingLinkAsync(CreateMeetingLinkRequest request, CancellationToken ct = default);
    Task<MeetingRecordingResponse> StartRecordingAsync(StartRecordingRequest request, CancellationToken ct = default);
    Task<MeetingRecordingResponse> StopRecordingAsync(StopRecordingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingRecordingResponse>> ListRecordingsAsync(string? meetingId, CancellationToken ct = default);
    Task<MeetingRecordingResponse?> GetRecordingAsync(string recordingId, CancellationToken ct = default);
    Task AttachTranscriptionAsync(string? meetingId, TranscriptionJobResponse transcription, CancellationToken ct = default);
    Task AttachTranscriptContentAsync(
        string jobId,
        string? transcriptText,
        string? speakerFormattedTranscript,
        string? transcriptFileName,
        string? transcriptFileUrl,
        CancellationToken ct = default);
}
