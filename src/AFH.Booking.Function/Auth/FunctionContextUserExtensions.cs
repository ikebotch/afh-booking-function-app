using AFH.Booking.Application.Models.Auth;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;

namespace AFH.Booking.Function.Auth;

public static class FunctionContextUserExtensions
{
    private const string DomainUserPrincipalKey = "DomainUserPrincipal";
    private const string DomainUserContextKey = "DomainUserContext";

    public static void SetDomainUserPrincipal(this FunctionContext context, ClaimsPrincipal principal, AdviserUserContext? user = null)
    {
        context.Items[DomainUserPrincipalKey] = principal;
        if (user is not null)
            context.Items[DomainUserContextKey] = user;
    }

    public static ClaimsPrincipal? GetDomainUserPrincipal(this FunctionContext context)
    {
        return context.Items.TryGetValue(DomainUserPrincipalKey, out var value)
            ? value as ClaimsPrincipal
            : null;
    }

    public static AdviserUserContext? GetDomainUserContext(this FunctionContext context)
    {
        return context.Items.TryGetValue(DomainUserContextKey, out var value)
            ? value as AdviserUserContext
            : null;
    }
}
