namespace AFH.Booking.Domain.Location;

public sealed class BufferInfo
{
    public int TravelBufferMinutes { get; set; }
    public int CompanyBufferMinutes { get; set; }
    public int PreMeetingBufferMinutes { get; set; }
    public int PostMeetingBufferMinutes { get; set; }
    public int MaxTravelTimeMinutes { get; set; }
}
