using AFH.Booking.Infrastructure.Persistence.Models;

public sealed class CalendarEventSnapshotModel
{
    public string Id { get; set; } = default!;            
    public string ReceiptId { get; set; } = default!;     

    public string UserId { get; set; } = default!;   
    public string ProviderEventId { get; set; } = default!;

    public string? CalendarId { get; set; }
    public string? Subject { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool? IsCancelled { get; set; }

    public DateTime FetchedUtc { get; set; }
    public string? FetchError { get; set; }

    public string? ChangeKey { get; set; }              
    public string? ICalUId { get; set; }

    public CalendarNotificationReceiptModel Receipt { get; set; } = default!;

}