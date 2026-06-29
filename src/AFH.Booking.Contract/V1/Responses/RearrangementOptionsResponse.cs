namespace AFH.Booking.Contracts.V1.Responses;

public sealed class RearrangementOptionsResponse
{
    public string BookingId { get; init; } = default!;
    public string? BookingReference { get; init; }
    public string TransactionId { get; init; } = default!;
    public string AssignedAdviserId { get; init; } = default!;
    public string AssignedAdviserName { get; init; } = default!;

    public bool AssignedAdviserHasAvailability { get; init; }

    public GetAvailabilityResponse AssignedAdviserOptions { get; init; } = new();
    public GetAvailabilityResponse AlternativeAdviserOptions { get; init; } = new();
}
