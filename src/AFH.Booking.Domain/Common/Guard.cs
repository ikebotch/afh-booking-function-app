namespace AFH.Booking.Domain.Common;

public static class Guard
{
    public static string NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{paramName} is required.");

        return value;
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new DomainException(message);
    }

    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new DomainException($"{paramName} is required.");

        return value;
    }
}
