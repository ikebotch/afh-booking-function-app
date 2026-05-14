namespace AFH.Booking.Domain.Availability;

public sealed class SlotScoreBreakdown
{
   
    public Dictionary<string, int> Parts { get; } = new();

    public int Total => Parts.Values.Sum();

    public void Add(string reason, int value)
    {
        reason = Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        Parts[reason] = value;
    }
}
