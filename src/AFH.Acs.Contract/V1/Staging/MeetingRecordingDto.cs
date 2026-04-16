namespace AFH.Acs.Recorder.DTOs;

public class MeetingRecordingDto
{

    public string RecordingId { get; set; } = default!;
    public string MeetingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string BlobName { get; set; } = default!;
    public string BlobUrl { get; set; } = default!;

    public DateTimeOffset RecordingStartUtc { get; set; }
    public DateTimeOffset RecordingEndUtc { get; set; }
    public int? DurationSeconds { get; set; }

    public string? AdviserName { get; set; }
    public string? ClientName { get; set; }
    public DateTime MeetingDate { get; set; }
    public string ClientEntityID { get; set; } = default!;
    public string? Filename { get; set; }
    public string? AttitudetoRisk { get; set; }
    public string? FinancialGoals { get; set; }
    public string? Tax { get; set; }
    public string? MeetingType { get; set; }
    public string? MeetingTitle { get; set; }
    public string? NotesStatus { get; set; }
    public string? AdviserEmail { get; set; }
    public string? ContentType { get; set; }

    public string? Transcription { get; set; } = default!;
}