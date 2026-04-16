namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class MeetingEntity
{
    public string MeetingId { get; set; } = string.Empty;
    public string LeadId { get; set; } = string.Empty;
    public string AdviserId { get; set; } = string.Empty;
    public string? AdviserName { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public string CalendarEventReference { get; set; } = string.Empty;
    public string MeetingType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ClientEmail { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public bool ConsentToRecording { get; set; }
    public DateTime? ConsentTimestampUtc { get; set; }
    public string Status { get; set; } = "SCHEDULED";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<MeetingAttendeeEntity> Attendees { get; set; } = new List<MeetingAttendeeEntity>();
    public ICollection<MeetingRecordingEntity> Recordings { get; set; } = new List<MeetingRecordingEntity>();
    public MeetingTranscriptionEntity? Transcription { get; set; }
}
