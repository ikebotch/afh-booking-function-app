namespace AFH.Acs.Recorder.DTOs;

public class AdviserListItemDto
{
    /// <summary>
    /// Internal adviser identifier (Snowflake primary key or XPlan/CRM reference).
    /// </summary>
    public string AdviserId { get; set; } = default!;

    /// <summary>
    /// Display name shown to clients and LeadTech users.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Adviser’s AFH work email address.
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// Region the adviser belongs to (London, North, Midlands, etc.).
    /// Used for regional filtering in the booking workflow.
    /// </summary>
    public string Region { get; set; } = default!;

    /// <summary>
    /// Whether the adviser participates in the LeadTech programme.
    /// Only these advisers should appear in the booking UI.
    /// </summary>
    public bool IsLeadTechAdviser { get; set; }

    /// <summary>
    /// A small “profile card” description for the adviser.
    /// (Optional, used for the UI.)
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// Client-facing photo URL (optional).
    /// </summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// Advisor availability summary for the next 30 days.
    /// This is populated from MS Graph /calendarView.
    /// </summary>
    public AdviserAvailabilityDto Availability { get; set; } = new();
}