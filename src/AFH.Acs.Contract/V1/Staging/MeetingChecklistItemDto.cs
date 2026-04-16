namespace AFH.Acs.Recorder.DTOs;

public class MeetingChecklistItemDto
{
    public string ItemId { get; set; } = default!;

    /// <summary>
    /// The meeting this checklist item belongs to.
    /// </summary>
    public string MeetingId { get; set; } = default!;

    /// <summary>
    /// Display label, e.g. “Verify ID”, “Confirm ATR”, etc.
    /// </summary>
    public string Label { get; set; } = default!;

    /// <summary>
    /// Optional category, e.g. “Compliance”, “Pre-meeting”, “Post-meeting”.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether this checklist item is complete.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// UTC timestamp for when it was completed (if applicable).
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>
    /// Ordering for the checklist within the meeting UI.
    /// </summary>
    public int DisplayOrder { get; set; }
}