using AFH.Booking.Domain.Bookings.Queries;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IAdminBookingSearchService
{
    Task<Result<AdminBookingSearchResponse>> SearchAsync(SearchAdminBookingsQuery query, CancellationToken ct);
}
