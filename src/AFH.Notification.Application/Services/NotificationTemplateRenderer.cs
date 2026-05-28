using System.Reflection;
using System.Text.RegularExpressions;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;

namespace AFH.Notification.Application.Services;

public sealed partial class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    private readonly IReadOnlyList<INotificationTemplatePolicy> _policies;
    private readonly INotificationTemplateStore? _templateStore;

    public NotificationTemplateRenderer(
        IEnumerable<INotificationTemplatePolicy> policies,
        INotificationTemplateStore? templateStore = null)
    {
        _policies = policies.ToArray();
        _templateStore = templateStore;
    }

    public async Task<NotificationTemplateRenderResult> RenderAsync(
        NotificationRequested notification,
        CancellationToken ct)
    {
        var parsed = await ResolveTemplateAsync(notification, ct);
        var body = ReplaceTokens(parsed.Body, notification.Data);
        var subject = ReplaceTokens(parsed.Subject, notification.Data);

        return new NotificationTemplateRenderResult(
        [
            new NotificationChannelContent(
                parsed.Channel,
                subject,
                HtmlBody: null,
                body)
        ]);
    }

    private async Task<TemplateParts> ResolveTemplateAsync(NotificationRequested notification, CancellationToken ct)
    {
        if (TryGetExplicitTemplate(notification, out var templateKey, out var templateVersion, out var channel))
        {
            if (_templateStore is not null)
            {
                var dbTemplate = await _templateStore.GetAsync(templateKey, templateVersion, channel, ct);
                if (dbTemplate is not null)
                {
                    return new TemplateParts(
                        dbTemplate.SubjectTemplate ?? string.Empty,
                        dbTemplate.Channel,
                        dbTemplate.BodyTemplate);
                }
            }

            var templateName = $"{notification.Type.SourceApplication}.{templateKey}.{templateVersion}.txt";
            return ParseTemplate(await LoadTemplateAsync(templateName, ct));
        }

        return ParseTemplate(await LoadTemplateAsync(GetTemplateName(notification), ct));
    }

    private static bool TryGetExplicitTemplate(
        NotificationRequested notification,
        out string templateKey,
        out string templateVersion,
        out NotificationChannel channel)
    {
        templateKey = string.Empty;
        templateVersion = string.Empty;
        channel = default;

        if (!notification.Data.TryGetValue("TemplateKey", out var key) ||
            !notification.Data.TryGetValue("TemplateVersion", out var version) ||
            string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var requestedChannels = notification.Recipients
            .SelectMany(x => x.PreferredChannels ?? [])
            .Distinct()
            .ToArray();

        if (requestedChannels.Length != 1)
            return false;

        templateKey = key.Trim();
        templateVersion = version.Trim();
        channel = requestedChannels[0];
        return true;
    }

    private string GetTemplateName(NotificationRequested notification)
    {
        if (notification.Data.TryGetValue("TemplateKey", out var templateKey) &&
            notification.Data.TryGetValue("TemplateVersion", out var templateVersion) &&
            !string.IsNullOrWhiteSpace(templateKey) &&
            !string.IsNullOrWhiteSpace(templateVersion))
        {
            return $"{notification.Type.SourceApplication}.{templateKey.Trim()}.{templateVersion.Trim()}.txt";
        }

        return _policies.FirstOrDefault(policy => policy.CanHandle(notification.Type))?.GetTemplateName(notification.Type)
               ?? throw new NotSupportedException($"Notification template '{notification.Type}' is not supported yet.");
    }

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
