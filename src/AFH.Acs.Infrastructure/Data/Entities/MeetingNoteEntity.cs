using System.ComponentModel.DataAnnotations;

namespace AFH.Acs.Recorder.Infrastructure.Data.Entities;

public class MeetingNoteEntity
{
    [Key]
    public string NoteId { get; set; } = default!;
    public string MeetingId { get; set; } = default!;
    public string AdviserId { get; set; } = default!;

    public string NoteText { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public MeetingEntity Meeting { get; set; } = default!;
    public AdviserEntity Adviser { get; set; } = default!;
}