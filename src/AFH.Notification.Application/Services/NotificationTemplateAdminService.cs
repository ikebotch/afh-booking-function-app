using AFH.Notification.Application.Abstractions;
using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Services;

public sealed class NotificationTemplateAdminService : INotificationTemplateAdminService
{
    private readonly INotificationTemplateAdminStore _store;

    public NotificationTemplateAdminService(INotificationTemplateAdminStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<NotificationTemplateSummary>> ListAsync(NotificationTemplateQuery query, CancellationToken ct)
        => _store.ListAsync(query, ct);

    public Task<NotificationTemplateAdminItem?> GetAsync(Guid id, CancellationToken ct)
        => _store.GetAsync(id, ct);

    public Task<NotificationTemplateAdminItem?> GetAsync(string templateKey, string templateVersion, NotificationChannel channel, CancellationToken ct)
        => _store.GetAsync(templateKey, templateVersion, channel, ct);

    public async Task<NotificationTemplateAdminItem> CreateAsync(NotificationTemplateUpsert template, CancellationToken ct)
    {
        Validate(template);
        if (await _store.ExistsAsync(template.TemplateKey.Trim(), template.TemplateVersion.Trim(), template.Channel, excludingId: null, ct))
            throw new NotificationRequestValidationException("TemplateKey, TemplateVersion and Channel must be unique.");

        return await _store.CreateAsync(Normalize(template), ct);
    }

    public async Task<NotificationTemplateAdminItem> UpdateAsync(Guid id, NotificationTemplateUpsert template, CancellationToken ct)
    {
        Validate(template);
        if (await _store.ExistsAsync(template.TemplateKey.Trim(), template.TemplateVersion.Trim(), template.Channel, excludingId: id, ct))
            throw new NotificationRequestValidationException("TemplateKey, TemplateVersion and Channel must be unique.");

        return await _store.UpdateAsync(id, Normalize(template), ct);
    }

    public Task<NotificationTemplateAdminItem> SetActiveAsync(Guid id, bool isActive, string? actor, CancellationToken ct)
        => _store.SetActiveAsync(id, isActive, actor, ct);

    private static void Validate(NotificationTemplateUpsert template)
    {
        if (string.IsNullOrWhiteSpace(template.TemplateKey))
            throw new NotificationRequestValidationException("TemplateKey is required.");
        if (string.IsNullOrWhiteSpace(template.TemplateVersion))
            throw new NotificationRequestValidationException("TemplateVersion is required.");
        if (template.Channel == NotificationChannel.Unknown)
            throw new NotificationRequestValidationException("Channel is required.");
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new NotificationRequestValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(template.BodyTemplate))
            throw new NotificationRequestValidationException("BodyTemplate is required.");
        if (template.Channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(template.SubjectTemplate))
            throw new NotificationRequestValidationException("SubjectTemplate is required for Email templates.");
        if (string.IsNullOrWhiteSpace(template.ContentType))
            throw new NotificationRequestValidationException("ContentType is required.");
    }

    private static NotificationTemplateUpsert Normalize(NotificationTemplateUpsert template)
        => template with
        {
            TemplateKey = template.TemplateKey.Trim(),
            TemplateVersion = template.TemplateVersion.Trim(),
            Name = template.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(template.Description) ? null : template.Description.Trim(),
            SubjectTemplate = string.IsNullOrWhiteSpace(template.SubjectTemplate) ? null : template.SubjectTemplate,
            BodyTemplate = template.BodyTemplate,
            ContentType = template.ContentType.Trim(),
            Actor = string.IsNullOrWhiteSpace(template.Actor) ? null : template.Actor.Trim()
        };
}
