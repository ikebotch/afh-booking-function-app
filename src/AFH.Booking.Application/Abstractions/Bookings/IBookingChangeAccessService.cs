namespace AFH.Booking.Application.Abstractions.Bookings;

public interface IBookingChangeAccessService
{
    Task<Result<BookingChangeActorContext>> ValidateClientTokenAsync(
        string bookingId,
        string? token,
        CancellationToken ct);

    Task<Result<BookingAccessLinkResponse>> CreateClientLinkAsync(
        CreateBookingAccessLinkRequest request,
        CancellationToken ct);

    Task<Result<BookingAccessLinkResponse>> ResendClientLinkAsync(
        CreateBookingAccessLinkRequest request,
        CancellationToken ct);
}

public sealed record BookingChangeActorContext(
    string ActorType,
    string? ActorId,
    string? TransactionRef,
    string? CorrelationId = null,
    string? CurrentBookingId = null);

public sealed class CreateBookingAccessLinkRequest
{
    public string BookingId { get; init; } = string.Empty;
    public string? ActorId { get; init; }
    public string? CreatedBy { get; init; }
    public int? ExpiryHours { get; init; }
}

public sealed class BookingAccessLinkResponse
{
    public string LinkId { get; init; } = string.Empty;
    public string BookingId { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string? AccessUrl { get; init; }
    public DateTimeOffset ExpiresUtc { get; init; }
    public string? TransactionRef { get; init; }
}
