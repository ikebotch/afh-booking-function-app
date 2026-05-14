namespace AFH.Booking.Domain.Location.Travel;

public sealed class TravelToNearestOffice
{
    public int? EtaMinutes { get; set; }
    public decimal? DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
    public string OfficeId { get; set; } = "";

}


