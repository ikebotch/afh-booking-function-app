using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Functions.Auth;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Booking.Functions.Middleware;

public sealed class DomainUserAuthMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string[] ProtectedRoutePrefixes =
    [
        "/api/v1/me"
    ];

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        var path = request.Url.AbsolutePath;
        if (!ProtectedRoutePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            context.GetInvocationResult().Value = await request.ProblemAsync(HttpStatusCode.Unauthorized, "Missing Authorization header.", CancellationToken.None, Errors.Unauthorized);
            return;
        }

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.GetInvocationResult().Value = await request.ProblemAsync(HttpStatusCode.Unauthorized, "Authorization header must use Bearer.", CancellationToken.None, Errors.Unauthorized);
            return;
        }

        var validator = context.InstanceServices.GetRequiredService<IEntraTokenValidator>();
        var validation = await validator.ValidateAsync(authHeader["Bearer ".Length..].Trim(), CancellationToken.None);

        if (!validation.IsSuccess || validation.Principal is null)
        {
            var statusCode = string.Equals(validation.ErrorCode, "Forbidden", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.Forbidden
                : string.Equals(validation.ErrorCode, "ServerError", StringComparison.OrdinalIgnoreCase)
                    ? HttpStatusCode.InternalServerError
                    : HttpStatusCode.Unauthorized;

            context.GetInvocationResult().Value = await request.ProblemAsync(
                statusCode,
                validation.ErrorMessage ?? "Request failed.",
                CancellationToken.None,
                validation.ErrorCode);
            return;
        }

        context.SetDomainUserPrincipal(validation.Principal);
        await next(context);
    }
}
