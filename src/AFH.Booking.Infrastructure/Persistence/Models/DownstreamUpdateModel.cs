namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class DownstreamUpdateModel
{
    public string Id { get; set; } = default!;
    public string BookingId { get; set; } = default!;
    public string ChangeType { get; set; } = default!;
    public string TransactionRef { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
}
