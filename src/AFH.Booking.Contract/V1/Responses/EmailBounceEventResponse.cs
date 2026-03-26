namespace AFH.Booking.Contracts.V1.Responses;

public sealed class EmailBounceEventResponse
{
    public string BounceId { get; init; } = default!;
    public string? ProviderMessageId { get; init; }
    public string? RecipientEmail { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public DateTime OccurredUtc { get; init; }
    public DateTime ReceivedUtc { get; init; }
}
