using AFH.Booking.Application.Models.Auth;

namespace AFH.Booking.Infrastructure.Auth;

internal static class MockAdviserUserContextFactory
{
    private const string MockTokenPrefix = "mock-token:";

    public static AdviserUserContext? TryCreate(string? token)
    {
        if (!CanUseMockTokens())
            return null;

        var userId = TryReadMockUserId(token);
        return userId?.ToLowerInvariant() switch
        {
            "mock-platform-admin" => Create(
                userId,
                "platform.admin@afh.co.uk",
                "Platform Admin",
                "Platform administrator",
                null,
                ["Platform Admin"],
                ["*"],
                [Scope("*", "All", null, "All records")]),

            "mock-booking-manager" => Create(
                userId,
                "booking.manager@afh.co.uk",
                "Booking Manager",
                "Booking operations",
                null,
                ["Booking Manager"],
                [
                    "Dashboard.Read",
                    "Bookings.Admin.Read",
                    "Bookings.Cancel.AsPartner",
                    "Bookings.RearrangementOptions.Read",
                    "Bookings.Rearrange.AsPartner",
                    "Bookings.Rearrange.Direct",
                    "Bookings.Approvals.Read",
                    "Bookings.Approvals.Review",
                    "Calendar.Read",
                    "Notifications.Read"
                ],
                [Scope("Bookings", "Organisation", null, "Organisation bookings")]),

            "mock-adviser" => Create(
                userId,
                "john.doe@afh.co.uk",
                "John Doe",
                "Adviser",
                "ADV-001",
                ["Adviser"],
                ["Dashboard.Read", "Calendar.Read"],
                [
                    Scope("Bookings", "AdviserSelf", "ADV-001", "Own bookings"),
                    Scope("Calendar", "AdviserSelf", "ADV-001", "Own calendar"),
                    Scope("Advisers", "AdviserSelf", "ADV-001", "Own adviser profile")
                ]),

            "mock-auditor" => Create(
                userId,
                "audit.reader@afh.co.uk",
                "Audit Reader",
                "Reporting and audit",
                null,
                ["Auditor"],
                [
                    "Dashboard.Read",
                    "Bookings.Admin.Read",
                    "Advisers.Read",
                    "Calendar.Read",
                    "Notifications.Read",
                    "Reporting.Read",
                    "Audit.Read",
                    "System.Audit.Read",
                    "System.Health.Read"
                ],
                [Scope("*", "Organisation", null, "Organisation records")]),

            _ => null
        };
    }

    private static AdviserUserContext Create(
        string userId,
        string email,
        string displayName,
        string jobRole,
        string? adviserId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        IReadOnlyList<AdviserUserAccessScope> accessScopes)
        => new()
        {
            UserId = userId,
            ExternalSubject = userId,
            Email = email,
            DisplayName = displayName,
            JobRole = jobRole,
            AdviserId = adviserId,
            Roles = roles,
            Permissions = permissions,
            AccessScopes = accessScopes
        };

    private static AdviserUserAccessScope Scope(
        string area,
        string scopeType,
        string? scopeValue,
        string displayName)
        => new()
        {
            Area = area,
            ScopeType = scopeType,
            ScopeValue = scopeValue,
            DisplayName = displayName
        };

    private static string? TryReadMockUserId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        const string bearerPrefix = "Bearer ";
        var value = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..].Trim()
            : token.Trim();

        return value.StartsWith(MockTokenPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[MockTokenPrefix.Length..].Trim()
            : null;
    }

    private static bool CanUseMockTokens()
    {
        var explicitAllow = Environment.GetEnvironmentVariable("Booking__AllowMockTokens");
        if (string.Equals(explicitAllow, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        var environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
