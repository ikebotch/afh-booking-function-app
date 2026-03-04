using AFH.Booking.Infrastructure.Options;
namespace AFH.Booking.Infrastructure.Options;

public sealed class BookingDbOptions
{
    public const string SectionName = "BookingDb";

    public string ConnectionString { get; init; } = default!;
}