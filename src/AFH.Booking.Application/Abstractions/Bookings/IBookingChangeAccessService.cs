using AFH.Booking.Application.Models.Bookings;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IBookingChangeAccessService
{
    Task<Result<BookingChangeActorContext>> ValidateClientTokenAsync(
        string bookingId,
        string? token,
        CancellationToken ct);

    Task<Result<string>> GenerateClientTokenAsync(
        string bookingId,
        CancellationToken ct);
}
