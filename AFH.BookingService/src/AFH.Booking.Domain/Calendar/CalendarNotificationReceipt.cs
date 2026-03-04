namespace AFH.Booking.Domain.Calendar;

public sealed class CalendarNotificationReceipt
{
    public string Id { get; }
    public string SubscriptionId { get; }
    public string EventId { get; }
    public string? ChangeType { get; }
    public string? ClientState { get; }
    public bool Accepted { get; }
    public string? RejectReason { get; }
    public DateTime ReceivedUtc { get; }
    public string? RawPayload { get; }

    private CalendarNotificationReceipt(
        string id,
        string subscriptionId,
        string eventId,
        string? changeType,
        string? clientState,
        bool accepted,
        string? rejectReason,
        DateTime receivedUtc,
        string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new DomainException("id is required.");
        if (string.IsNullOrWhiteSpace(subscriptionId)) throw new DomainException("subscriptionId is required.");
        if (string.IsNullOrWhiteSpace(eventId)) throw new DomainException("eventId is required.");

        if (!accepted && string.IsNullOrWhiteSpace(rejectReason))
            throw new DomainException("rejectReason is required when notification is rejected.");

        Id = id;
        SubscriptionId = subscriptionId;
        EventId = eventId;
        ChangeType = changeType;
        ClientState = clientState;
        Accepted = accepted;
        RejectReason = rejectReason;
        ReceivedUtc = DateTime.SpecifyKind(receivedUtc, DateTimeKind.Utc);
        RawPayload = rawPayload;
    }

    public static CalendarNotificationReceipt Create(
        string subscriptionId,
        string eventId,
        string? changeType,
        string? clientState,
        bool accepted,
        string? rejectReason,
        DateTime receivedUtc,
        string? rawPayload = null)
        => new(
            id: Guid.NewGuid().ToString("N"),
            subscriptionId: subscriptionId.Trim(),
            eventId: eventId.Trim(),
            changeType: string.IsNullOrWhiteSpace(changeType) ? null : changeType.Trim(),
            clientState: string.IsNullOrWhiteSpace(clientState) ? null : clientState.Trim(),
            accepted: accepted,
            rejectReason: string.IsNullOrWhiteSpace(rejectReason) ? null : rejectReason.Trim(),
            receivedUtc: receivedUtc,
            rawPayload: rawPayload);
}