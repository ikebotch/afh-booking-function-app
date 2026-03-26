namespace AFH.Booking.Contracts.V1.Requests;

public sealed class EmailBounceWebhookRequest
{
    public string? ProviderMessageId { get; init; }
    public string? RecipientEmail { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public DateTime? OccurredUtc { get; init; }
}
