using System.Text.RegularExpressions;
using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;

namespace AFH.Notification.Application.Services;

public sealed partial class NotificationTemplatePreviewService : INotificationTemplatePreviewService
{
    private readonly INotificationTemplateStore _templateStore;

    public NotificationTemplatePreviewService(INotificationTemplateStore templateStore)
    {
        _templateStore = templateStore;
    }

    public async Task<NotificationTemplatePreviewResult> PreviewAsync(NotificationTemplatePreviewRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            throw new NotificationRequestValidationException("TemplateKey is required.");
        if (string.IsNullOrWhiteSpace(request.TemplateVersion))
            throw new NotificationRequestValidationException("TemplateVersion is required.");
        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            var template = await _templateStore.GetAsync(request.TemplateKey.Trim(), request.TemplateVersion.Trim(), request.Channel, ct)
                ?? throw new NotificationRequestValidationException("Template was not found or is inactive.");

            return Render(
                template.SubjectTemplate,
                template.BodyTemplate,
                request.Data,
                template.TemplateKey,
                template.TemplateVersion,
                template.Channel);
        }

        return Render(
            request.SubjectTemplate,
            request.BodyTemplate,
            request.Data,
            request.TemplateKey.Trim(),
            request.TemplateVersion.Trim(),
            request.Channel);
    }

    private static NotificationTemplatePreviewResult Render(
        string? subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string> data,
        string templateKey,
        string templateVersion,
        Contract.V1.Dtos.NotificationChannel channel)
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var subject = subjectTemplate is null ? null : ReplaceTokens(subjectTemplate, data, missing);
        var body = ReplaceTokens(bodyTemplate, data, missing);

        return new NotificationTemplatePreviewResult(subject, body, missing.ToArray(), templateKey, templateVersion, channel);
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> data, ISet<string> missing)
        => TokenRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (data.TryGetValue(key, out var value))
                return value;

            missing.Add(key);
            return string.Empty;
        });

    [GeneratedRegex(@"\{\{(?<key>[A-Za-z0-9_]+)\}\}")]
    private static partial Regex TokenRegex();
}
