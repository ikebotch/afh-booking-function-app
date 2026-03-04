namespace AFH.Booking.Contracts.Dtos;

public sealed class BookingSummaryDto
{
    public string BookingId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string Status { get; set; } = string.Empty;
}