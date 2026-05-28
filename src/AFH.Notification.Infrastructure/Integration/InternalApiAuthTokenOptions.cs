namespace AFH.Notification.Infrastructure.Integration;

public sealed class InternalApiAuthTokenOptions
{
    public const string SectionName = "InternalApiAuth";

    public string? Token { get; set; }
}
