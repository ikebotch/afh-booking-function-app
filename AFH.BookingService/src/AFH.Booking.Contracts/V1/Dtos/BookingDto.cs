namespace AFH.Booking.Contracts.V1.Dtos;


public sealed class BookingDto
{
    public string BookingId { get; init; } = default!;
    public string AdviserId { get; init; } = default!;
    public string AdviserName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string Mode { get; init; } = default!;
    public string CustomerId { get; init; } = default!;
    public string Subject { get; init; } = default!;
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string Timezone { get; init; } = default!;
    public string? Notes { get; init; }
    public string? ProviderEventId { get; init; }
    public DateTime? HoldExpiresUtc { get; init; }
    public string? OnlineMeetingJoinUrl { get; init; }
    public LocationDto? Location { get; init; }
}