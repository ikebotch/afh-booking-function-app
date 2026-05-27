namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationActor(
    NotificationActorType Type,
    string? Id,
    string? DisplayName,
    string? Email);
