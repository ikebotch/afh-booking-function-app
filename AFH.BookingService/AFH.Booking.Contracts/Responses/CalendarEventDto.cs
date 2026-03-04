namespace AFH.Booking.Contracts.Responses;


public sealed class CalendarEventDto
{
    public string AdviserId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public string Subject { get; init; } = default!;

    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }

    public bool IsAllDay { get; init; }
    public bool IsCancelled { get; init; }

    public bool IsBusy { get; init; }

    public string? Organizer { get; init; }

    public IReadOnlyList<string> Attendees { get; init; }
        = Array.Empty<string>();
}