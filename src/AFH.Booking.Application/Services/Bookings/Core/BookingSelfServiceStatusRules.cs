using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Application.Bookings;

internal static class BookingSelfServiceStatusRules
{
    public static bool CanUseActionLinks(BookingHoldStatus status)
        => status is BookingHoldStatus.Active or BookingHoldStatus.Confirmed;

    public static Result EnsureActionable(BookingHold hold, string action)
    {
        if (CanUseActionLinks(hold.Status))
            return Result.Ok();

        return Result.Fail(
            HttpStatusCode.Conflict,
            $"Booking '{hold.Id}' is {hold.Status} and cannot be {action}.",
            Errors.Conflict);
    }
}
