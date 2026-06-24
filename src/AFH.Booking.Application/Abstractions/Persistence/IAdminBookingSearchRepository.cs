using AFH.Booking.Domain.Bookings.Queries;

namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IAdminBookingSearchRepository
{
    Task<AdminBookingSearchResult> SearchAsync(SearchAdminBookingsQuery query, CancellationToken ct);
}

public sealed class AdminBookingSearchResult
{
    public IReadOnlyList<AdminBookingSearchItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}
