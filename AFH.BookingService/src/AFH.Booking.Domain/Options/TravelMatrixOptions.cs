namespace AFH.Booking.Domain.Options;

public sealed class TravelMatrixOptions
{
    public const string SectionName = "TravelMatrix";

    public bool Enabled { get; set; } = false;
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
