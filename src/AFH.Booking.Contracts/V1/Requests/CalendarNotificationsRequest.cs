using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Requests;

/// <summary>
/// Calendar notification webhook payload:
/// { "value": [ { ... } ] }
/// </summary>
public sealed class CalendarNotificationsRequest
{
    [JsonPropertyName("value")]
    public List<CalendarNotificationItemDto> Value { get; set; } = [];
}

public sealed class CalendarNotificationItemDto
{
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("clientState")]
    public string? ClientState { get; set; }

    [JsonPropertyName("changeType")]
    public string? ChangeType { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; set; }

    [JsonPropertyName("subscriptionExpirationDateTime")]
    public DateTimeOffset? SubscriptionExpirationDateTime { get; set; }

    [JsonPropertyName("resourceData")]
    public CalendarNotificationResourceDataDto? ResourceData { get; set; }
}

public sealed class CalendarNotificationResourceDataDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; set; }

    [JsonPropertyName("@odata.id")]
    public string? ODataId { get; set; }
}
