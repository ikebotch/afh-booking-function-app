using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;


public class MeetingEntity
{
    [Key]
    public string MeetingId { get; set; } = default!;
    public string LeadId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string GraphEventId { get; set; } = default!;
    public string MeetingType { get; set; } = default!;
    public string Title { get; set; } = default!;

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public string ClientEmail { get; set; } = default!;
    public bool ConsentToRecording { get; set; }
    public DateTime? ConsentTimestampUtc { get; set; }

    public string Status { get; set; } = "SCHEDULED";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public AdviserEntity Adviser { get; set; } = default!;
    public LeadEntity Lead { get; set; } = default!;

    public ICollection<MeetingAttendeeEntity> Attendees { get; set; } = new List<MeetingAttendeeEntity>();
    public ICollection<MeetingRecordingEntity> Recordings { get; set; } = new List<MeetingRecordingEntity>();
    public MeetingTranscriptionEntity? Transcription { get; set; }
    public MeetingAtrAnalysisEntity? AtrAnalysis { get; set; }
}