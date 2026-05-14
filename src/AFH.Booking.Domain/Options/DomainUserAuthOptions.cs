namespace AFH.Booking.Domain.Options;

public sealed class DomainUserAuthOptions
{
    public const string SectionName = "DomainUserAuth";

    public bool Enabled { get; set; } = true;
    public string? TenantId { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public List<string> AllowedTenantIds { get; set; } = [];
    public List<string> AllowedEmailDomains { get; set; } = [];
    public List<DomainRoleMappingOptions> RoleMappings { get; set; } = [];
}

public sealed class DomainRoleMappingOptions
{
    public string Role { get; set; } = string.Empty;
    public List<string> AppRoles { get; set; } = [];
    public List<string> Groups { get; set; } = [];
    public List<string> Emails { get; set; } = [];
    public List<string> Domains { get; set; } = [];
}
