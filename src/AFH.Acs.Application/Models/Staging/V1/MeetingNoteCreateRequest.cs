using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Models.V1;

public class MeetingNoteCreateRequest
{
    /// <summary>
    /// Title or heading for the note.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// Body content (markdown or plain text).
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// Author name or adviser ID/email.
    /// </summary>
    public string Author { get; set; } = default!;
}