using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationChannelContent(
    NotificationChannel Channel,
    string? Subject,
    string? HtmlBody,
    string TextBody,
    string ContentType = "text/plain");
