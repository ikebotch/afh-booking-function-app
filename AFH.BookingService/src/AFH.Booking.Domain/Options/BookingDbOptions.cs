namespace AFH.Booking.Domain.Options;

public sealed class BookingDbOptions
{
    public const string SectionName = "BookingDb";

    public string ConnectionString { get; set; } = string.Empty;
}
