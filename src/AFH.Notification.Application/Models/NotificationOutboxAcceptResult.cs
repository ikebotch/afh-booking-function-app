namespace AFH.Notification.Application.Models;

public sealed record NotificationOutboxAcceptResult(
    IReadOnlyList<NotificationOutboxCreateResult> Items)
{
    public Guid RequestId => Items.Count == 0 ? Guid.Empty : Items[0].Item.Id;
    public bool CreatedAny => Items.Any(item => item.Created);
}
