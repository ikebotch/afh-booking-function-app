namespace AFH.Booking.Contracts.V1.Dtos;

public sealed class CalendarViewDto
{
    public string AdviserId { get; init; } = default!;
    public string ProviderEventId { get; init; } = default!;
    public string Subject { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public bool IsBusy { get; init; }
    public bool IsCancelled { get; init; }
    public bool IsOnlineMeeting { get; init; }
    public string? OnlineMeetingJoinUrl { get; init; }
    public IReadOnlyList<string>? Categories { get; init; }

    public bool MailboxUnavailable { get; init; }
    public string Message { get; init; }

    public List<CalendarBlock> Conflicts { get; init; } = [];
}


public sealed class CalendarBlock
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? Subject { get; init; }
}