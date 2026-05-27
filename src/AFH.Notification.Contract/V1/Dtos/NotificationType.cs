namespace AFH.Notification.Contract.V1.Dtos;

public sealed record NotificationType(
    string SourceApplication,
    string Name)
{
    public override string ToString()
        => $"{SourceApplication}:{Name}";
}
