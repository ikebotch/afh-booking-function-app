using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Users;

[BookingOpenApiTag("Users")]
public sealed class GetCurrentUserFunction
{
    private readonly ICurrentUserProfileResolver _profileResolver;

    public GetCurrentUserFunction(ICurrentUserProfileResolver profileResolver)
    {
        _profileResolver = profileResolver;
    }

    [Function("Users_GetCurrentUser")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var principal = context.GetDomainUserPrincipal();
        if (principal is null)
        {
            return await req.ProblemAsync(HttpStatusCode.Unauthorized, "User context was not available.", ct, Errors.Unauthorized);
        }

        var profile = _profileResolver.Resolve(principal);
        if (profile.Roles.Count == 0)
        {
            return await req.ProblemAsync(HttpStatusCode.Forbidden, "Signed-in user does not have a mapped Booking domain role.", ct, Errors.Forbidden);
        }

        return await req.OkJsonAsync(new CurrentUserResponse
        {
            UserId = profile.UserId,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            Roles = profile.Roles,
            Capabilities = profile.Capabilities
        }, ct);
    }
}
