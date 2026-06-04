namespace AFH.Booking.Domain.Bookings.Commands;

public sealed class GetRearrangementOptionsCommand
{
    public string BookingId { get; set; } = default!;
    public BookingActorContext? ActorContext { get; set; }
    public string? PreferredStartUtc { get; set; }
    public int? Duration { get; set; }
    public bool? IsRemote { get; set; }
    public string? MeetingType { get; set; }
    public int? Limit { get; set; }
    public string? Cursor { get; set; }
}
