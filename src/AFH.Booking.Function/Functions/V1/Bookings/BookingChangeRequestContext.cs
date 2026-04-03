using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Common;
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

        return req.Headers.TryGetValues("Authorization", out var authValues)
            ? authValues.FirstOrDefault()
            : null;
    }

    public static async Task<Result<BookingChangeActorContext>> ValidateClientAsync(
        HttpRequestData req,
        string bookingId,
        IBookingChangeAccessService accessService,
        CancellationToken ct)
    {
        return await accessService.ValidateClientTokenAsync(bookingId, GetClientAccessToken(req), ct);
    }
}
