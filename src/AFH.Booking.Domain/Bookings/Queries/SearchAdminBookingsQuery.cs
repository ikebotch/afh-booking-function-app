namespace AFH.Booking.Domain.Bookings.Queries;

public sealed class SearchAdminBookingsQuery
{
    public string? BookingId { get; init; }
    public string? TransactionId { get; init; }
    public string? TransactionRef { get; init; }
    public string? Status { get; init; }
    public string? AdviserId { get; init; }
    public string? ClientRef { get; init; }
    public string? LocationRef { get; init; }
    public string? MeetingType { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
