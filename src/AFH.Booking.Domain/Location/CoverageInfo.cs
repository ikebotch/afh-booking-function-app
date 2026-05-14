namespace AFH.Booking.Domain.Location;





public sealed class CoverageInfo
{
    public bool WithinCoverage { get; set; }
    public string AnchorPostcode { get; set; } = "";
    public decimal DistanceMiles { get; set; }
}





