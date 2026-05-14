namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class EmailBounceEventModel
{
    public string Id { get; set; } = default!;
    public string? ProviderMessageId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonDetail { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime ReceivedUtc { get; set; }
}
