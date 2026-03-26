using System.Text.Json;

namespace AFH.Booking.Infrastructure.Logging;

public static class ApplicationLogPayloadHelper
{
    public static string? Serialize(object? payload, ApplicationLoggingOptions options)
    {
        if (!options.LogPayloads || payload is null)
            return null;

        try
        {
            var json = JsonSerializer.Serialize(payload);
            return Truncate(json, options.MaxPayloadLength);
        }
        catch
        {
            return null;
        }
    }

    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (maxLength <= 0 || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
