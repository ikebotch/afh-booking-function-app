namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationActor(
    string ActorType,
    string SourceApplication,
    string? Id,
    string? DisplayName,
    string? Email);
