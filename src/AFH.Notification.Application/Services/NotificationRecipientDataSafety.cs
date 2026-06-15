using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

internal static class NotificationRecipientDataSafety
{
    private static readonly string[] ClientOnlyDataKeys =
    [
        "token",
        "selfServiceToken",
        "viewUrl",
        "cancelUrl",
        "rearrangeUrl",
        "rescheduleUrl",
        "bookingSelfServiceToken",
        "bookingChangeToken",
        "viewBookingUrl",
        "cancelBookingUrl",
        "rescheduleBookingUrl",
        "rearrangeBookingUrl",
        "manageBookingLinks"
    ];

    private static readonly string[] RecipientTemplateEligibleKeys =
    [
        "booking-confirmed",
        "booking-cancelled",
        "booking-rescheduled"
    ];

    public static NotificationRequested ForRecipientChannel(
        NotificationRequested notification,
        NotificationRecipient recipient,
        NotificationChannel channel)
    {
        return notification with
        {
            Recipients =
            [
                recipient with
                {
                    PreferredChannels = [channel]
                }
            ],
            Data = BuildRecipientData(notification, recipient, channel)
        };
    }

    public static IReadOnlyDictionary<string, string> BuildRecipientData(
        NotificationRequested notification,
        NotificationRecipient recipient,
        NotificationChannel channel)
    {
        var includeClientLinks = IsClientRecipient(recipient.RecipientType);
        var data = notification.Data
            .Where(kvp => includeClientLinks || !IsClientOnlyDataKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        data["RecipientType"] = recipient.RecipientType;
        data["recipientType"] = recipient.RecipientType;

        NormalizeChannelTemplateKey(data, channel, recipient.RecipientType);
        return data;
    }

    private static void NormalizeChannelTemplateKey(
        IDictionary<string, string> data,
        NotificationChannel channel,
        string recipientType)
    {
        var templateKeyName = $"TemplateKey:{channel}";
        var templateVersionName = $"TemplateVersion:{channel}";
        var templateKey = TryGet(data, templateKeyName) ?? TryGet(data, "TemplateKey");
        var templateVersion = TryGet(data, templateVersionName) ?? TryGet(data, "TemplateVersion");

        if (string.IsNullOrWhiteSpace(templateKey) || string.IsNullOrWhiteSpace(templateVersion))
            return;

        data[templateKeyName] = IsClientRecipient(recipientType)
            ? templateKey.Trim()
            : ResolveRecipientTemplateKey(templateKey, recipientType);
        data[templateVersionName] = templateVersion.Trim();
        data["TemplateKey"] = data[templateKeyName];
        data["TemplateVersion"] = data[templateVersionName];
    }

    private static string ResolveRecipientTemplateKey(string templateKey, string recipientType)
    {
        var trimmed = templateKey.Trim();
        if (!RecipientTemplateEligibleKeys.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return trimmed;

        var suffix = NormalizeRecipientType(recipientType);
        return string.IsNullOrWhiteSpace(suffix) ? trimmed : $"{trimmed}-{suffix}";
    }

    private static string? TryGet(IDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool IsClientRecipient(string recipientType)
        => recipientType.Equals("Client", StringComparison.OrdinalIgnoreCase);

    private static bool IsClientOnlyDataKey(string key)
        => key.Contains("token", StringComparison.OrdinalIgnoreCase)
           || ClientOnlyDataKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeRecipientType(string recipientType)
    {
        var normalized = new string(recipientType
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "contactcentre" => "contact-centre",
            "operationsmanager" => "operations-manager",
            "reportingmanager" => "reporting-manager",
            "orgadmin" => "admin",
            _ => normalized
        };
    }
}
