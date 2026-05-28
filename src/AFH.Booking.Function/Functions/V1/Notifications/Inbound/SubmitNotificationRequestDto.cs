using System.Text.Json;
using System.Text.Json.Serialization;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Booking.Function.Functions.V1.Notifications.Inbound;

internal sealed record SubmitNotificationRequestDto
{
    public NotificationType? Type { get; init; }
    public string? CorrelationId { get; init; }
    public NotificationActor? Actor { get; init; }
    public IReadOnlyList<SubmitNotificationRecipientDto>? Recipients { get; init; }
    public IReadOnlyDictionary<string, string>? Data { get; init; }
    public string? SourceApplication { get; init; }

    [JsonPropertyName("notificationType")]
    public string? NotificationTypeName { get; init; }

    public string? SourceReferenceType { get; init; }
    public string? SourceReferenceId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? TemplateKey { get; init; }
    public string? TemplateVersion { get; init; }
    public IReadOnlyList<JsonElement>? Channels { get; init; }

    public NotificationRequested ToNotificationRequested()
    {
        var data = Data is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(Data);

        AddIfPresent(data, nameof(SourceReferenceType), SourceReferenceType);
        AddIfPresent(data, nameof(SourceReferenceId), SourceReferenceId);
        AddIfPresent(data, nameof(IdempotencyKey), IdempotencyKey);
        AddIfPresent(data, nameof(TemplateKey), TemplateKey);
        AddIfPresent(data, nameof(TemplateVersion), TemplateVersion);

        var defaultChannels = ParseChannels(Channels, "channels");
        var recipients = Recipients?
            .Select(recipient => recipient.ToNotificationRecipient(defaultChannels))
            .ToArray();

        var type = Type ?? CreateTypeFromFlatProperties();

        return new NotificationRequested(
            type!,
            CorrelationId!,
            Actor!,
            recipients!,
            data);
    }

    private NotificationType? CreateTypeFromFlatProperties()
    {
        if (SourceApplication is null && NotificationTypeName is null)
            return null;

        return new NotificationType(
            SourceApplication ?? string.Empty,
            NotificationTypeName ?? string.Empty);
    }

    private static void AddIfPresent(IDictionary<string, string> data, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            data[key] = value;
    }

    internal static NotificationChannel[]? ParseChannels(IReadOnlyList<JsonElement>? values, string fieldName)
        => values is null
            ? null
            : values.Select(value => ParseChannel(value, fieldName))
                .Where(channel => channel != NotificationChannel.Unknown)
                .Distinct()
                .ToArray();

    private static NotificationChannel ParseChannel(JsonElement value, string fieldName)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (Enum.TryParse<NotificationChannel>(raw, ignoreCase: true, out var channel) &&
                channel != NotificationChannel.Unknown)
            {
                return channel;
            }
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var numericChannel) &&
            Enum.IsDefined(typeof(NotificationChannel), numericChannel))
        {
            var channel = (NotificationChannel)numericChannel;
            if (channel != NotificationChannel.Unknown)
                return channel;
        }

        throw new NotificationRequestValidationException(
            $"{fieldName} must contain only Email, Sms or Push.");
    }
}

internal sealed record SubmitNotificationRecipientDto
{
    public string? RecipientType { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? MobileNumber { get; init; }
    public string? PushTarget { get; init; }
    public IReadOnlyList<JsonElement>? PreferredChannels { get; init; }
    public IReadOnlyList<JsonElement>? Channels { get; init; }

    public NotificationRecipient ToNotificationRecipient(NotificationChannel[]? defaultChannels)
    {
        var channels = SubmitNotificationRequestDto.ParseChannels(
            PreferredChannels ?? Channels,
            "recipient channels") ?? defaultChannels;

        return new NotificationRecipient(
            RecipientType ?? string.Empty,
            DisplayName,
            Email,
            MobileNumber,
            PushTarget,
            channels);
    }
}
