namespace AFH.Booking.Domain.ValueObjects;

public sealed record BookingTransactionId
{
    public string Value { get; }

    public BookingTransactionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("TransactionId cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;
}