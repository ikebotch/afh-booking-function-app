namespace AFH.Booking.Domain.Bookings;

public readonly record struct BookingId(string Value)
{
    public override string ToString() => Value;

    public static BookingId New() => new(Guid.NewGuid().ToString("N"));

    public static BookingId From(string value) => new(value);
}
