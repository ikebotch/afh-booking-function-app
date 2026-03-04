namespace AFH.Booking.Domain.Common;

public static class Guard
{
    public static string NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{paramName} is required.");

        return value;
    }

    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new DomainException($"{paramName} is required.");

        return value;
    }

    public static DateTime MustBeUtc(DateTime value, string paramName)
    {
        // If you strictly store all times as UTC, enforce here.
        // In practice, DateTime.Kind may come in as Unspecified, so keep this soft.
        // Uncomment if you want strict enforcement:
        // if (value.Kind != DateTimeKind.Utc) throw new DomainException($"{paramName} must be UTC.");
        return value;
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new DomainException(message);
    }
}
