namespace AFH.Acs.Function.Options;

public sealed class InternalApiAuthOptions
{
    public const string SectionName = "InternalApiAuth";
    public string Token { get; set; } = string.Empty;
    public bool AllowAnonymousInDevelopment { get; set; }
}
