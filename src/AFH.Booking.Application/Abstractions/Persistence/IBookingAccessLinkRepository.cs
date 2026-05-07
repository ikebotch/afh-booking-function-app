namespace AFH.Booking.Application.Abstractions.Persistence;

public interface IBookingAccessLinkRepository
{
    Task AddAsync(BookingAccessLinkRecord link, CancellationToken ct);
    Task<BookingAccessLinkRecord?> GetAsync(string linkId, CancellationToken ct);
    Task RevokeActiveForBookingAsync(string bookingId, DateTime revokedUtc, string reason, CancellationToken ct);
}

public sealed class BookingAccessLinkRecord
{
    public string Id { get; init; } = string.Empty;
    public string OriginalBookingId { get; init; } = string.Empty;
    public string CurrentBookingId { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public string ActorType { get; init; } = string.Empty;
    public string? ActorId { get; init; }
    public string? TransactionRef { get; init; }
    public DateTime ExpiresUtc { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? RevokedUtc { get; init; }
    public string? RevokedReason { get; init; }
}
