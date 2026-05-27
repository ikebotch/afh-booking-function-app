using System.Reflection;
using System.Text.RegularExpressions;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed partial class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public async Task<NotificationTemplateRenderResult> RenderAsync(
        NotificationRequested notification,
        CancellationToken ct)
    {
        var template = await LoadTemplateAsync(GetTemplateName(notification.Type), ct);
        var parsed = ParseTemplate(template);
        var body = ReplaceTokens(parsed.Body, notification.Data);

        return new NotificationTemplateRenderResult(
        [
            new NotificationChannelContent(
                parsed.Channel,
                parsed.Subject,
                HtmlBody: null,
                body)
        ]);
    }

    private static string GetTemplateName(NotificationType notificationType)
        => notificationType switch
        {
            NotificationType.BookingConfirmed => "Booking.booking-confirmed.v1.txt",
            NotificationType.BookingRescheduled => "Booking.booking-rescheduled.v1.txt",
            NotificationType.BookingCancelled => "Booking.booking-cancelled.v1.txt",
            _ => throw new NotSupportedException($"Notification template '{notificationType}' is not supported yet.")
        };

    private static async Task<string> LoadTemplateAsync(string templateName, CancellationToken ct)
    {
        var assembly = typeof(NotificationTemplateRenderer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith(templateName, StringComparison.Ordinal));

        if (resourceName is null)
            throw new InvalidOperationException($"Notification template resource '{templateName}' was not found.");

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Notification template resource '{templateName}' could not be opened.");
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync(ct);
    }

    private static TemplateParts ParseTemplate(string template)
    {
        var normalised = template.Replace("\r\n", "\n", StringComparison.Ordinal);
        var split = normalised.Split("\n---\n", 2, StringSplitOptions.None);
        if (split.Length != 2)
            throw new InvalidOperationException("Notification template metadata delimiter was not found.");

        var metadata = split[0]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

        var subject = metadata.TryGetValue("subject", out var subjectValue)
            ? subjectValue
            : throw new InvalidOperationException("Notification template subject is required.");

        var channel = metadata.TryGetValue("channel", out var channelValue) &&
                      Enum.TryParse<NotificationChannel>(channelValue, ignoreCase: true, out var parsedChannel)
            ? parsedChannel
            : throw new InvalidOperationException("Notification template channel is required.");

        return new TemplateParts(subject, channel, split[1].TrimEnd('\n'));
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> data)
    {
        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return data.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }

    [GeneratedRegex(@"\{\{(?<key>[A-Za-z0-9_]+)\}\}")]
    private static partial Regex TokenRegex();

    private sealed record TemplateParts(
        string Subject,
        NotificationChannel Channel,
        string Body);
}
