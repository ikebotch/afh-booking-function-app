namespace AFH.Acs.Recorder.DTOs;

public class MeetingDetailsDto
{
    public string MeetingId { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public string? AdviserName { get; set; }

    public string LeadId { get; set; } = default!;
    public string MeetingType { get; set; } = default!;
    public string Title { get; set; } = default!;

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }

    public string ClientEmail { get; set; } = default!;
    public string? ClientName { get; set; }

    public bool ConsentToRecording { get; set; }
    public DateTimeOffset? ConsentTimestampUtc { get; set; }

    public string Status { get; set; } = "Scheduled";

    public List<MeetingAttendeeDto> Attendees { get; set; } = new();
    public List<MeetingRecordingDto> Recordings { get; set; } = new();
    public MeetingTranscriptionDto? Transcription { get; set; }
}