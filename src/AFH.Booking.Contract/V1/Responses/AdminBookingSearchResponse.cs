namespace AFH.Booking.Contracts.V1.Responses;

public sealed class AdminBookingSearchResponse
{
    public IReadOnlyList<AdminBookingSearchItem> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class AdminBookingSearchItem
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string SlotId { get; init; } = default!;
    public string TransactionId { get; init; } = default!;
    public string TransactionRef { get; init; } = default!;
    public string ClientRef { get; init; } = default!;
    public string? ClientName { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientAddressLine1 { get; init; }
    public string? ClientAddressLine2 { get; init; }
    public string? ClientTown { get; init; }
    public string? ClientCounty { get; init; }
    public string? ClientPostcode { get; init; }
    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsRemote { get; init; }
    public string? MeetingType { get; init; }
    public string? LocationRef { get; init; }
    public string Status { get; init; } = default!;
    public DateTime CreatedUtc { get; init; }
    public DateTime? ConfirmedUtc { get; init; }
    public DateTime? CancelledUtc { get; init; }
    public string? CancelReason { get; init; }
}
