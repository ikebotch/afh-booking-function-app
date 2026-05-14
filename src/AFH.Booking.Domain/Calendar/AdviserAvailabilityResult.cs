namespace AFH.Booking.Domain.Calendar;

public sealed class AdviserAvailabilityResult
{
    public bool IsFree { get; init; }
    public bool MailboxUnavailable { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<CalendarConflictBlock> Conflicts { get; init; } = Array.Empty<CalendarConflictBlock>();
}

public sealed class CalendarConflictBlock
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? Subject { get; init; }
    public string? ProviderEventId { get; init; }
}