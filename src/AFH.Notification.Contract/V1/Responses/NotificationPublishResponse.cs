namespace AFH.Notification.Contract.V1.Responses;

public sealed record NotificationPublishResponse(
    string CorrelationId,
    string Status);
