namespace AFH.Booking.Infrastructure.Persistence.Models;


public sealed class CalendarNotificationReceiptModel
{
    public string Id { get; set; } = default!;
    public string SubscriptionId { get; set; } = default!;
    public string EventId { get; set; } = default!;
    public string? ChangeType { get; set; }
    public string? ClientState { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public string? RawPayload { get; set; }
    public bool Accepted { get; set; }
    public string? RejectReason { get; set; }
    public byte[] RowVersion { get; set; } = default!;

    public ICollection<CalendarEventSnapshotModel> Snapshots { get; set; } = new List<CalendarEventSnapshotModel>();
}