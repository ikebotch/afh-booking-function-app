using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;

namespace AFH.Booking.Function.Auth;

public static class FunctionContextUserExtensions
{
    private const string DomainUserPrincipalKey = "DomainUserPrincipal";

    public static void SetDomainUserPrincipal(this FunctionContext context, ClaimsPrincipal principal)
    {
        context.Items[DomainUserPrincipalKey] = principal;
    }

    public static ClaimsPrincipal? GetDomainUserPrincipal(this FunctionContext context)
    {
        return context.Items.TryGetValue(DomainUserPrincipalKey, out var value)
            ? value as ClaimsPrincipal
            : null;
    }
}
