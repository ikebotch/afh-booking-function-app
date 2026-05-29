
namespace AFH.Notification.Infrastructure.Options;

public sealed class NotificationDbOptions
{
    public const string SectionName = "BookingDb";
    public string ConnectionString { get; set; } = string.Empty;
}