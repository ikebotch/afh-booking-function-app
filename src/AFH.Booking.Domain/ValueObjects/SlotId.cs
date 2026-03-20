namespace AFH.Booking.Domain.ValueObjects;

public sealed class SlotId : IEquatable<SlotId>
{
    public string Value { get; }

    private SlotId(string value)
    {
        Value = Guard.NotNullOrWhiteSpace(value, nameof(value));
    }

    public static SlotId From(string value) => new(value);

    public bool Equals(SlotId? other)
        => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SlotId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
