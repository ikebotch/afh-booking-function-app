using AFH.Acs.Domain.Enums;

namespace AFH.Acs.Domain.Entities;

public sealed class MeetingSession
{
    public string MeetingId { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string AdviserId { get; init; } = string.Empty;
    public string? AdviserName { get; init; }
    public string LeadId { get; init; } = string.Empty;
    public string MeetingType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public string ClientEmail { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public bool ConsentToRecording { get; init; }
    public DateTimeOffset? ConsentTimestampUtc { get; init; }
    public MeetingSessionStatus Status { get; init; } = MeetingSessionStatus.Scheduled;
    public string CalendarEventReference { get; init; } = string.Empty;
    public IReadOnlyList<MeetingAttendee> Attendees { get; init; } = [];
    public IReadOnlyList<MeetingRecordingArtifact> Recordings { get; init; } = [];
    public MeetingTranscriptionArtifact? Transcription { get; init; }

    public void EnsureScheduleWindowIsValid()
    {
        if (string.IsNullOrWhiteSpace(AdviserId))
            throw new InvalidOperationException("AdviserId is required.");

        if (string.IsNullOrWhiteSpace(LeadId))
            throw new InvalidOperationException("LeadId is required.");

        if (string.IsNullOrWhiteSpace(ClientEmail))
            throw new InvalidOperationException("ClientEmail is required.");

        if (EndUtc <= StartUtc)
            throw new InvalidOperationException("EndUtc must be greater than StartUtc.");
    }
}
