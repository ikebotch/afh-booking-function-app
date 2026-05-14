namespace AFH.Booking.Domain.Bookings.Score;

public sealed class SlotScoreResult
{
    public int Score { get; init; }
    public IReadOnlyDictionary<string, int> Breakdown { get; init; } =
        new Dictionary<string, int>();
}