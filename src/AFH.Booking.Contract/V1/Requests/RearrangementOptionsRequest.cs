namespace AFH.Booking.Contracts.V1.Requests;

public sealed class RearrangementOptionsRequest
{
    public string? PreferredStartUtc { get; init; }
    public int? Duration { get; init; }
    public bool? IsRemote { get; init; }
    public string? MeetingType { get; init; }
    public int? Limit { get; init; }
    public string? Cursor { get; init; }
}
