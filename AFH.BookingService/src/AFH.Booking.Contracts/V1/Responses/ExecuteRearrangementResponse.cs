namespace AFH.Booking.Contracts.V1.Responses;

public sealed class ExecuteRearrangementResponse
{
    public string PreviousBookingId { get; init; } = default!;
    public string? NewBookingId { get; init; }
    public string? NewSlotId { get; init; }
    public string Status { get; init; } = default!;
    public bool ApprovalRequired { get; init; }
    public string? RoutedTo { get; init; }
    public string? ApprovedBy { get; init; }
    public IReadOnlyList<string> ChangeSummary { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NotificationChannels { get; init; } = Array.Empty<string>();
}
