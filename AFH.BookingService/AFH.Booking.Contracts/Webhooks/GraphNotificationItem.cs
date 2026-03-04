namespace AFH.Common.CalendarUtils.Sdk.Contracts.Webhooks;

public sealed class GraphNotificationItem
{
    public string? SubscriptionId { get; set; }
    public string? ChangeType { get; set; }        // created|updated|deleted
    public string? Resource { get; set; }          // users/{id}/events/{eventId}
    public string? ClientState { get; set; }
    public GraphResourceData? ResourceData { get; set; }
}
