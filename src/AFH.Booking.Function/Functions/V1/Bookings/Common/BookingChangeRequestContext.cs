using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Models.Bookings;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Bookings;

internal static class BookingChangeRequestContext
{
    public static string? GetCorrelationId(HttpRequestData req)
    {
        return req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault()
            : null;
    }

    public static string? GetClientAccessToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("x-booking-access-token", out var customValues))
            return customValues.FirstOrDefault();

        if (req.Headers.TryGetValues("Authorization", out var authValues))
            return authValues.FirstOrDefault();

        return GetQueryValue(req.Url.Query, "token");
    }

    public static async Task<Result<BookingChangeActorContext>> ValidateClientAsync(
        HttpRequestData req,
        string bookingId,
        IBookingChangeAccessService accessService,
        CancellationToken ct)
    {
        return await accessService.ValidateClientTokenAsync(bookingId, GetClientAccessToken(req), ct);
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var trimmed = query[0] == '?' ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var rawKey = separator >= 0 ? pair[..separator] : pair;
            if (!string.Equals(Uri.UnescapeDataString(rawKey), key, StringComparison.OrdinalIgnoreCase))
                continue;

            var rawValue = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            return Uri.UnescapeDataString(rawValue.Replace("+", "%2B", StringComparison.Ordinal));
        }

        return null;
    }
}
