namespace AFH.Acs.Recorder.Models.V1;

public class MeetingNoteUpdateRequest
{
    /// <summary>
    /// Updated title of the note.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Updated content.
    /// </summary>
    public string Content { get; set; } = default!;
}