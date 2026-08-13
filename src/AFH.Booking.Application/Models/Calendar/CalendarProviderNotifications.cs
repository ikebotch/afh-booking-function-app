namespace AFH.Booking.Application.Models.Calendar;

public sealed class CalendarProviderNotificationEnvelope
{
    public IReadOnlyList<CalendarProviderNotificationItem>? Value { get; init; }
}

public sealed class CalendarProviderNotificationItem
{
    public string? SubscriptionId { get; init; }
    public string? ChangeType { get; init; }
    public string? Resource { get; init; }
    public string? ClientState { get; init; }
    public CalendarProviderResourceData? ResourceData { get; init; }
}

public sealed class CalendarProviderResourceData
{
    public string? Id { get; init; }
    public string? OdataType { get; init; }
}

public sealed class CalendarProviderNotificationProcessingResult
{
    public int Received { get; init; }
    public int Ignored { get; init; }
    public int Corrected { get; init; }
    public int Restored { get; init; }
    public int FlaggedForOperations { get; init; }
    public IReadOnlyList<CalendarProviderNotificationItemResult> Items { get; init; } = [];
}

public sealed class CalendarProviderNotificationItemResult
{
    public string? ProviderEventId { get; init; }
    public string? BookingId { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
