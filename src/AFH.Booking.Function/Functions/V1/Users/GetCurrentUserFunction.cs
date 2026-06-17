using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.Booking.Function.Functions.V1.Users;

[BookingOpenApiTag("Users")]
public sealed class GetCurrentUserFunction
{
    private readonly IAdviserUserContextClient _userContextClient;

    public GetCurrentUserFunction(IAdviserUserContextClient userContextClient)
    {
        _userContextClient = userContextClient;
    }

    [Function("Users_GetCurrentUser")]
    [BookingOpenApiOperation(
        "Users",
        "Get current user",
        ResponseType = typeof(CurrentUserResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequestData req,
        FunctionContext context,
        CancellationToken ct)
    {
        var bearerToken = GetBearerToken(req);
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return await req.ProblemAsync(HttpStatusCode.Unauthorized, "Bearer token was not available.", ct, Errors.Unauthorized);
        }

        var profile = await _userContextClient.GetCurrentUserAsync(bearerToken, ct);
        if (profile is null)
        {
            return await req.ProblemAsync(HttpStatusCode.Forbidden, "Signed-in user is not mapped in Location Identity.", ct, Errors.Forbidden);
        }

        return await req.OkJsonAsync(new CurrentUserResponse
        {
            UserId = profile.UserId,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            Roles = profile.Roles,
            Capabilities = profile.Permissions
        }, ct);
    }

    private static string? GetBearerToken(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            return null;

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : null;
    }
}
