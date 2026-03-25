namespace AFH.Booking.Application.Abstractions.Governance;

public sealed record BookingConflictCheckResult(
    bool IsBlocked,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<BookingConflictDetail> Details);

public sealed record BookingConflictDetail(
    string Code,
    string Message,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    string? ProviderEventId = null);
