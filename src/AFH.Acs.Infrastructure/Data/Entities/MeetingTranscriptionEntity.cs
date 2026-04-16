using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;
public class MeetingTranscriptionEntity
{
    [Key]
    public string TranscriptionId { get; set; } = default!;
    public string MeetingId { get; set; } = default!;
    public string RecordingId { get; set; } = default!;

    public string Language { get; set; } = "en-GB";
    public string RawJson { get; set; } = default!;   // raw transcription JSON
    public string FullText { get; set; } = default!;
    public string? SummaryText { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
    public MeetingRecordingEntity Recording { get; set; } = default!;
}