using AFH.Booking.Domain.Bookings.Queries;

namespace AFH.Booking.Application.Services.Bookings.Core;

public sealed class AdminBookingSearchService : IAdminBookingSearchService
{
    private const int MaxPageSize = 100;
    private const string PendingRescheduleStatus = "PendingReschedule";
    private readonly IAdminBookingSearchRepository _repository;

    public AdminBookingSearchService(IAdminBookingSearchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<AdminBookingSearchResponse>> SearchAsync(SearchAdminBookingsQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<AdminBookingSearchResponse>.Fail(HttpStatusCode.BadRequest, "page must be greater than zero.", Errors.Validation);

        if (query.PageSize is < 1 or > MaxPageSize)
            return Result<AdminBookingSearchResponse>.Fail(HttpStatusCode.BadRequest, $"pageSize must be between 1 and {MaxPageSize}.", Errors.Validation);

        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc.Value > query.ToUtc.Value)
            return Result<AdminBookingSearchResponse>.Fail(HttpStatusCode.BadRequest, "from must be before to.", Errors.Validation);

        var invalidStatus = query.Statuses.FirstOrDefault(status =>
            !string.Equals(status.Trim(), PendingRescheduleStatus, StringComparison.OrdinalIgnoreCase) &&
            !Enum.TryParse<BookingHoldStatus>(status.Trim(), true, out _));
        if (!string.IsNullOrWhiteSpace(invalidStatus))
        {
            return Result<AdminBookingSearchResponse>.Fail(HttpStatusCode.BadRequest, $"status '{invalidStatus}' is not valid.", Errors.Validation);
        }

        var result = await _repository.SearchAsync(query, ct);

        return Result<AdminBookingSearchResponse>.Ok(new AdminBookingSearchResponse
        {
            Items = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        });
    }
}
