namespace AFH.Booking.Contracts.V1.Responses;

public sealed class BookingAccessLinkResponse
{
    public string LinkId { get; init; } = string.Empty;
    public string BookingId { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string? AccessUrl { get; init; }
    public DateTimeOffset ExpiresUtc { get; init; }
    public string? TransactionRef { get; init; }
}
