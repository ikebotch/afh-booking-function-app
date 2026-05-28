namespace AFH.Notification.Application.Models;

public sealed class NotificationRequestValidationException : Exception
{
    public NotificationRequestValidationException(string message) : base(message)
    {
    }
}
