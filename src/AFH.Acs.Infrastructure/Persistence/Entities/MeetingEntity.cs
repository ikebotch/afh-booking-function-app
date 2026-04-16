namespace AFH.Acs.Infrastructure.Persistence.Entities;

public sealed class MeetingEntity
{
    public string MeetingId { get; set; } = string.Empty;
    public string LeadId { get; set; } = string.Empty;
    public string AdviserId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string GraphEventId { get; set; } = string.Empty;
    public string MeetingType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ClientEmail { get; set; } = string.Empty;
    public bool ConsentToRecording { get; set; }
    public DateTime? ConsentTimestampUtc { get; set; }
    public string Status { get; set; } = "SCHEDULED";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public AdviserEntity? Adviser { get; set; }
    public LeadEntity? Lead { get; set; }
    public ICollection<MeetingAttendeeEntity> Attendees { get; set; } = new List<MeetingAttendeeEntity>();
    public ICollection<MeetingRecordingEntity> Recordings { get; set; } = new List<MeetingRecordingEntity>();
    public MeetingTranscriptionEntity? Transcription { get; set; }
}
