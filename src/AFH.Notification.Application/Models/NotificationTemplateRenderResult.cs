namespace AFH.Notification.Application.Models;

public sealed record NotificationTemplateRenderResult(
    IReadOnlyList<NotificationChannelContent> ChannelContent);
