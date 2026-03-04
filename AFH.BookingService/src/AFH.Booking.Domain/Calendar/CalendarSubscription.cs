namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarSubscription
{
    private CalendarSubscription() { }

    public string Id { get; private set; } = default!;
    public string SubscriptionId { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string Resource { get; private set; } = default!;
    public string NotificationUrl { get; private set; } = default!;
    public string? ClientState { get; private set; }
    public DateTime ExpirationUtc { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public static CalendarSubscription Create(
        string id,
        string subscriptionId,
        string userId,
        string resource,
        string notificationUrl,
        string? clientState,
        DateTime expirationUtc,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new DomainException("subscription id is required.");
        if (string.IsNullOrWhiteSpace(subscriptionId)) throw new DomainException("subscription id is required.");
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("userId is required.");
        if (string.IsNullOrWhiteSpace(resource)) throw new DomainException("resource is required.");
        if (string.IsNullOrWhiteSpace(notificationUrl)) throw new DomainException("notificationUrl is required.");

        return new CalendarSubscription
        {
            Id = id,
            SubscriptionId = subscriptionId,
            UserId = userId,
            Resource = resource,
            NotificationUrl = notificationUrl,
            ClientState = clientState,
            ExpirationUtc = DateTime.SpecifyKind(expirationUtc, DateTimeKind.Utc),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }

    public void Renew(DateTime newExpirationUtc, DateTime utcNow)
    {
        ExpirationUtc = DateTime.SpecifyKind(newExpirationUtc, DateTimeKind.Utc);
        UpdatedUtc = utcNow;
    }


    public static CalendarSubscription Rehydrate(
    string id,
   string subscriptionId,
    string userId,
    string resource,
    string notificationUrl,
    string? clientState,
    DateTime expirationUtc,
    DateTime createdUtc,
    DateTime updatedUtc)
    {
        return new CalendarSubscription
        {
            Id = id,
            SubscriptionId = subscriptionId,
            UserId = userId,
            Resource = resource,
            NotificationUrl = notificationUrl,
            ClientState = clientState,
            ExpirationUtc = DateTime.SpecifyKind(expirationUtc, DateTimeKind.Utc),
            CreatedUtc = DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc),
            UpdatedUtc = DateTime.SpecifyKind(updatedUtc, DateTimeKind.Utc)
        };
    }

    public void UpdateWebhook(string notificationUrl, string? clientState, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(notificationUrl))
            throw new DomainException("notificationUrl is required.");

        NotificationUrl = notificationUrl;
        ClientState = clientState;
        UpdatedUtc = utcNow;
    }
}