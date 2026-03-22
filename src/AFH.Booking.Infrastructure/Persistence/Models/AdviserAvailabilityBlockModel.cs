namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class AdviserAvailabilityBlockModel
{
    public string Id { get; set; } = default!;
    public string AdviserId { get; set; } = default!;
    public string ProviderEventId { get; set; } = default!;
    public string? CalendarId { get; set; }
    public string? Subject { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsCancelled { get; set; }
    public string? ChangeKey { get; set; }
    public string? ICalUId { get; set; }
    public DateTime LastSyncedUtc { get; set; }
    public string? SourceReceiptId { get; set; }
}
