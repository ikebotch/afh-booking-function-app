namespace AFH.Booking.Domain.Auth;

public static class DomainUserRoles
{
    public const string Adviser = "Adviser";
    public const string Partner = "Partner";
    public const string Manager = "Manager";
    public const string Operations = "Operations";
    public const string Admin = "Admin";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Adviser,
        Partner,
        Manager,
        Operations,
        Admin
    };
}
