namespace AFH.Booking.Domain.ValueObjects;

// Score is 1..5 (best fit / ranking)
public sealed class Score : IEquatable<Score>
{
    public int Value { get; }

    public Score(int value)
    {
        Guard.True(value >= 1 && value <= 5, "Score must be between 1 and 5.");
        Value = value;
    }

    public static Score From(int value) => new(value);

    public bool Equals(Score? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is Score other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString();
}
