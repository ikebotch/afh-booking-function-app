namespace AFH.Acs.Recorder.Models;

public class MeetingNoteDto
{
    public string NoteId { get; set; } = default!;

    public string MeetingId { get; set; } = default!;

    /// <summary>
    /// Title or short summary of the note.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Full note content (markdown/text).
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// Author of the note (typically the adviser).
    /// </summary>
    public string Author { get; set; } = default!;

    /// <summary>
    /// Timestamp when the note was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when the note was last modified (UTC).
    /// </summary>
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}