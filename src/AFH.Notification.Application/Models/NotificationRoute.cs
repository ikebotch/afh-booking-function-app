using AFH.Notification.Contract.V1.Dtos;

namespace AFH.Notification.Application.Models;

public sealed record NotificationRoute(
    IReadOnlyList<NotificationRecipient> Recipients,
    bool CopyContactCentre);
