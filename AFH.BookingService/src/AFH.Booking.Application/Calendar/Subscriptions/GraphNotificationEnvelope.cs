using System.Text.Json.Serialization;

namespace AFH.Booking.Application.Calendar.Subscriptions;

/// <summary>
/// Matches Microsoft Graph change notifications payload:
/// { "value": [ { ... } ] }
/// </summary>
public sealed class GraphNotificationEnvelope
{
    [JsonPropertyName("value")]
    public List<GraphNotificationItem> Value { get; set; } = [];
}

public sealed class GraphNotificationItem
{
    // Standard Graph fields
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("clientState")]
    public string? ClientState { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; } // created | updated | deleted

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }   // e.g. "users/{id}/events/{eventId}"

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("subscriptionExpirationDateTime")]
    public DateTimeOffset? SubscriptionExpirationDateTime { get; set; }

    // Resource data (often includes event id if includeResourceData is used,
    // or minimal id info depending on subscription configuration)
    [JsonPropertyName("resourceData")]
    public GraphNotificationResourceData? ResourceData { get; set; }
}

public sealed class GraphNotificationResourceData
{
    // Graph usually uses "id" for the entity id (event id)
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    // Some payloads include "@odata.type"
    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; set; }

    // Some payloads include "@odata.id"
    [JsonPropertyName("@odata.id")]
    public string? ODataId { get; set; }
}