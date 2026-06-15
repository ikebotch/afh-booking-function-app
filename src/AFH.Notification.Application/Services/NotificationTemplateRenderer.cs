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
        var parsed = await ResolveTemplatesAsync(notification, ct);

        return new NotificationTemplateRenderResult(
            parsed
                .Select(template =>
                {
                    var body = ReplaceTokens(template.Body, notification.Data);
                    var isHtml = string.Equals(template.ContentType, "text/html", StringComparison.OrdinalIgnoreCase);

                    return new NotificationChannelContent(
                        template.Channel,
                        string.IsNullOrWhiteSpace(template.Subject) ? null : ReplaceTokens(template.Subject, notification.Data),
                        HtmlBody: isHtml ? body : null,
                        body,
                        template.ContentType);
                })
                .ToArray());
    }

    private async Task<IReadOnlyList<TemplateParts>> ResolveTemplatesAsync(NotificationRequested notification, CancellationToken ct)
    {
        var requestedChannels = GetRequestedChannels(notification);
        var explicitTemplates = GetExplicitTemplates(notification, requestedChannels);
        if (explicitTemplates.Count > 0)
        {
            var resolved = new List<TemplateParts>(explicitTemplates.Count);
            foreach (var explicitTemplate in explicitTemplates)
            {
                var template = await TryResolveTemplateAsync(explicitTemplate, notification.Type.SourceApplication, ct);
                if (template is not null)
                {
                    resolved.Add(template);
                    continue;
                }

                resolved.Add(ParseTemplate(await LoadTemplateAsync(GetTemplateName(notification), ct)));
            }

            return resolved;
        }

        return [ParseTemplate(await LoadTemplateAsync(GetTemplateName(notification), ct))];
    }

    private async Task<TemplateParts?> TryResolveTemplateAsync(
        ExplicitTemplate explicitTemplate,
        string sourceApplication,
        CancellationToken ct)
    {
        if (_templateStore is not null)
        {
            var dbTemplate = await _templateStore.GetAsync(
                explicitTemplate.TemplateKey,
                explicitTemplate.TemplateVersion,
                explicitTemplate.Channel,
                ct);

            if (dbTemplate is not null)
            {
                return new TemplateParts(
                    dbTemplate.SubjectTemplate ?? string.Empty,
                    dbTemplate.Channel,
                    dbTemplate.BodyTemplate,
                    dbTemplate.ContentType);
            }
        }

        var templateName = $"{sourceApplication}.{explicitTemplate.TemplateKey}.{explicitTemplate.TemplateVersion}.txt";
        var embedded = await TryLoadTemplateAsync(templateName, ct);
        return embedded is null ? null : ParseTemplate(embedded);
    }

    private static IReadOnlyList<ExplicitTemplate> GetExplicitTemplates(
        NotificationRequested notification,
        IReadOnlyList<NotificationChannel> requestedChannels)
    {
        var templates = new List<ExplicitTemplate>();
        foreach (var channel in requestedChannels)
        {
            var key = GetDataValue(notification.Data, $"TemplateKey:{channel}") ??
                      GetDataValue(notification.Data, "TemplateKey");
            var version = GetDataValue(notification.Data, $"TemplateVersion:{channel}") ??
                          GetDataValue(notification.Data, "TemplateVersion");

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(version))
                templates.Add(new ExplicitTemplate(channel, key.Trim(), version.Trim()));
        }

        return templates;
    }

    private static IReadOnlyList<NotificationChannel> GetRequestedChannels(NotificationRequested notification)
        => notification.Recipients
            .SelectMany(x => x.PreferredChannels ?? [])
            .Where(channel => channel != NotificationChannel.Unknown)
            .Distinct()
            .ToArray();

    private static string? GetDataValue(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private string GetTemplateName(NotificationRequested notification)
        => _policies.FirstOrDefault(policy => policy.CanHandle(notification.Type))?.GetTemplateName(notification.Type)
           ?? throw new NotSupportedException($"Notification template '{notification.Type}' is not supported yet.");

    private static async Task<string> LoadTemplateAsync(string templateName, CancellationToken ct)
        => await TryLoadTemplateAsync(templateName, ct)
           ?? throw new InvalidOperationException($"Notification template resource '{templateName}' was not found.");

    private static async Task<string?> TryLoadTemplateAsync(string templateName, CancellationToken ct)
    {
        var assembly = typeof(NotificationTemplateRenderer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith(templateName, StringComparison.Ordinal));

        if (resourceName is null)
            return null;

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

        var contentType = metadata.TryGetValue("contentType", out var contentTypeValue) &&
                          !string.IsNullOrWhiteSpace(contentTypeValue)
            ? contentTypeValue
            : "text/plain";

        return new TemplateParts(subject, channel, split[1].TrimEnd('\n'), contentType);
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
        string Body,
        string ContentType);

    private sealed record ExplicitTemplate(
        NotificationChannel Channel,
        string TemplateKey,
        string TemplateVersion);
}
