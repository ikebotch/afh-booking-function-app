namespace AFH.Booking.Contracts.V1.Requests;

public sealed class UpsertMeetingTopicRequest
{
    public string? Label { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public int SortOrder { get; init; }
}
