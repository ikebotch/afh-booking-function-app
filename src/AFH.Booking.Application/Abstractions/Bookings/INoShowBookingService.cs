using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface INoShowBookingService
{
    Task<Result<RecordNoShowResponse>> HandleAsync(RecordNoShowCommand cmd, CancellationToken ct);
}