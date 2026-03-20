namespace AFH.Booking.Domain.Bookings.Score;

public sealed class SlotScoringContext
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }

    public bool IsRemote { get; init; }
    public int? TravelMinutes { get; init; }

    public bool AdviserPreferred { get; init; } // optional
}



