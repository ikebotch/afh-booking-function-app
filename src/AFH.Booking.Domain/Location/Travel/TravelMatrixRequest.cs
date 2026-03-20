namespace AFH.Booking.Domain.Location.Travel;



public sealed class TravelMatrixRequest
{
    public string RequestId { get; set; } = default!;
    public TravelMatrixMeeting Meeting { get; set; } = new();
    public TravelMatrixDestination Destination { get; set; } = new();
    public LocationFilters Filters { get; set; } = new();
}
