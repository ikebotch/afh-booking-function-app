namespace AFH.Notification.Infrastructure.Options;

public sealed class InternalApiAuthOptions
{
    public const string SectionName = "InternalApiAuth";

    public string? Token { get; set; }
    public bool AllowAnonymousInDevelopment { get; set; }
}
