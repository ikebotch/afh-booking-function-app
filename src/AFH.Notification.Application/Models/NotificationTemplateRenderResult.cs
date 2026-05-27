namespace AFH.Notification.Application.Models;

public sealed record NotificationTemplateRenderResult(
    string Subject,
    string HtmlBody,
    string TextBody);
