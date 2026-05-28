using AFH.Notification.Application.Models;
using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Abstractions;

public interface INotificationTemplateAdminStore
{
    Task<IReadOnlyList<NotificationTemplateSummary>> ListAsync(NotificationTemplateQuery query, CancellationToken ct);
    Task<NotificationTemplateAdminItem?> GetAsync(Guid id, CancellationToken ct);
    Task<NotificationTemplateAdminItem?> GetAsync(string templateKey, string templateVersion, NotificationChannel channel, CancellationToken ct);
    Task<bool> ExistsAsync(string templateKey, string templateVersion, NotificationChannel channel, Guid? excludingId, CancellationToken ct);
    Task<NotificationTemplateAdminItem> CreateAsync(NotificationTemplateUpsert template, CancellationToken ct);
    Task<NotificationTemplateAdminItem> UpdateAsync(Guid id, NotificationTemplateUpsert template, CancellationToken ct);
    Task<NotificationTemplateAdminItem> SetActiveAsync(Guid id, bool isActive, string? actor, CancellationToken ct);
}
