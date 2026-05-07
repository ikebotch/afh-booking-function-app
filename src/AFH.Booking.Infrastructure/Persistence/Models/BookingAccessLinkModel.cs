namespace AFH.Booking.Infrastructure.Persistence.Models;

public sealed class BookingAccessLinkModel
{
    public string Id { get; set; } = string.Empty;
    public string OriginalBookingId { get; set; } = string.Empty;
    public string CurrentBookingId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string? TransactionRef { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? RevokedReason { get; set; }
}
