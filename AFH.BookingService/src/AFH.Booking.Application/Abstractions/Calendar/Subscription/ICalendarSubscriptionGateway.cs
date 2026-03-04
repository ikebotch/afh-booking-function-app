namespace AFH.Booking.Application.Abstractions.Calendar.Subscription;

public interface ICalendarSubscriptionGateway
{


    Task<Result<CreateCalendarSubscriptionResult>> CreateAsync(
     CreateCalendarSubscriptionRequest request,
     CancellationToken ct);

    Task<Result> DeleteAsync(
        string subscriptionId,
        CancellationToken ct);


    Task<CreateCalendarSubscriptionResult> CreateOrRenewAsync(
         CreateCalendarSubscriptionRequest request,
         CancellationToken ct);
}




public sealed class CreateCalendarSubscriptionRequest
{
    public string UserId { get; set; } = default!;
    public string NotificationUrl { get; set; } = default!;
    public string ClientState { get; set; } = default!;
    public DateTime ExpirationUtc { get; set; }

    public string Resource { get; init; } = "/users/{userId}/events";
}

public sealed class CreateCalendarSubscriptionResult
{
    public string SubscriptionId { get; set; } = default!;
    public DateTimeOffset ExpirationUtc { get; set; }
    public string? Resource { get; set; }
}
