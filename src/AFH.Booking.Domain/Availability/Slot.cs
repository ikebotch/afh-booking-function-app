namespace AFH.Booking.Domain.Availability;

public sealed class Slot
{
    public SlotId Id { get; }
    public string AdviserId { get; }
    public TimeRange Window { get; }
    public Score Score { get; }

    public Slot(string adviserId, TimeRange window, Score score, SlotId? id = null)
    {
        AdviserId = Guard.NotNullOrWhiteSpace(adviserId, nameof(adviserId));
        Window = Guard.NotNull(window, nameof(window));
        Score = Guard.NotNull(score, nameof(score));
        Id = id ?? SlotId.From($"{AdviserId}:{Window.StartUtc:yyyyMMddHHmm}");
    }
}
