using AFH.Acs.Recorder.DTOs;

namespace AFH.Acs.Recorder.Services.Interface;
public interface IMeetingChecklistService
{
    /// <summary>
    /// Returns the checklist items instantiated for a given meeting.
    /// These are usually expanded from a template based on meeting type.
    /// </summary>
    Task<IReadOnlyList<MeetingChecklistItemDto>> GetChecklistAsync(
        string meetingId,
        CancellationToken ct = default);

    /// <summary>
    /// Sets completion flag for a specific checklist item on a meeting.
    /// </summary>
    Task<MeetingChecklistItemDto?> SetItemCompletionAsync(
        string meetingId,
        string itemId,
        bool isCompleted,
        CancellationToken ct = default);
}
