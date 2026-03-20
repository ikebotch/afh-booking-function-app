namespace AFH.Booking.Domain.Location.Travel;



public sealed class TravelMatrixMeeting
{
    public DateTime RequestedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int SearchHorizonMinutes { get; set; }
}
