using AFH.Acs.Recorder.Models;
using AFH.Acs.Recorder.Models.V1;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IMeetingNotesService
{
    /// <summary>
    /// Gets all notes for a meeting.
    /// </summary>
    Task<IReadOnlyList<MeetingNoteDto>> GetNotesAsync(
        string meetingId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new note for the meeting.
    /// </summary>
    Task<MeetingNoteDto> CreateNoteAsync(
        string meetingId,
        MeetingNoteCreateRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing note.
    /// </summary>
    Task<MeetingNoteDto?> UpdateNoteAsync(
        string meetingId,
        string noteId,
        MeetingNoteUpdateRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a note from the meeting.
    /// </summary>
    Task DeleteNoteAsync(
        string meetingId,
        string noteId,
        CancellationToken ct = default);
}
