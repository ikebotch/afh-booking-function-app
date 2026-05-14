namespace AFH.Booking.Contracts.V1.Requests.Config;

public sealed class UpsertMeetingTypeRequest
{
    public string? Label { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public int? DefaultDurationMinutes { get; init; }
    public int SortOrder { get; init; }
}