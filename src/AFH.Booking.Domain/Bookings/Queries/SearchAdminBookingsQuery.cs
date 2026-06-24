namespace AFH.Booking.Domain.Bookings.Queries;

public sealed class SearchAdminBookingsQuery
{
    public IReadOnlyList<string> BookingIds { get; init; } = [];
    public IReadOnlyList<string> TransactionIds { get; init; } = [];
    public IReadOnlyList<string> TransactionRefs { get; init; } = [];
    public IReadOnlyList<string> Statuses { get; init; } = [];
    public IReadOnlyList<string> AdviserIds { get; init; } = [];
    public IReadOnlyList<string> ClientRefs { get; init; } = [];
    public IReadOnlyList<string> LocationRefs { get; init; } = [];
    public IReadOnlyList<string> MeetingTypes { get; init; } = [];
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
