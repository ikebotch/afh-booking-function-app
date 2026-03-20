namespace AFH.Booking.Domain.ValueObjects;

public sealed class BookingId : IEquatable<BookingId>
{
    public string Value { get; }

    public BookingId(string value)
    {
        Value = Guard.NotNullOrWhiteSpace(value, nameof(value));
    }

    public static BookingId New() => new(Guid.NewGuid().ToString("N"));

    public static BookingId From(string value) => new(value);

    public override string ToString() => Value;

    public bool Equals(BookingId? other)
        => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is BookingId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
}
