using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings.Handlers;

public interface INoShowBookingHandler
{
    Task<Result<RecordNoShowResponse>> HandleAsync(RecordNoShowCommand cmd, CancellationToken ct);
}