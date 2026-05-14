namespace AFH.Booking.Domain.Location.Travel;

public sealed class TravelToClient
{
    public int? EtaMinutes { get; set; }
    public decimal? DistanceMiles { get; set; }
    public string Confidence { get; set; } = "Low";
}

