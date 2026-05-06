namespace AFH.Booking.Contracts.V1.Responses;

public sealed class GetClientResponse
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime? PreferredStartUtc { get; init; }
    public string? TransactionStatus { get; init; }
    public bool IsTransactionClosed { get; init; }
}
