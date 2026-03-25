namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IBookingChangeAccessService
{
    Task<Result<BookingChangeActorContext>> ValidateClientTokenAsync(
        string bookingId,
        string? token,
        CancellationToken ct);
}

public sealed record BookingChangeActorContext(
    string ActorType,
    string? ActorId,
    string? TransactionRef,
    string? CorrelationId = null);
