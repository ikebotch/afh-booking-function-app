namespace AFH.Booking.Contracts.Dtos;

public sealed class ScheduleDto
{
    public string AdviserId { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public List<BookingSummaryDto> Bookings { get; set; } = new();
}