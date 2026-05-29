namespace AFH.Notification.Infrastructure.Options;

public sealed class HttpNotificationPublisherOptions
{
    public const string SectionName = "Notifications:Integration:Http";

    public string? BaseUrl { get; set; } = "https://booking-service-bxbyewaagpg5bbcq.uksouth-01.azurewebsites.net";
    //public string? BaseUrl { get; set; } = "http://localhost:7071";
    public string RequestPath { get; set; } = "/api/v1/notifications/requests";
    public int TimeoutSeconds { get; set; } = 30;
    public string? FunctionKey { get; set; } = "afh-func-bk-m4BOirwrArsbvbPLCQ8pIOEeOotiC5_motpKUiUR-m0VOL1ww1cM_FsdWzuIujKC";
}
