using AFH.Booking.Domain.Options;
using AFH.Booking.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Functions.Middleware;

public sealed class InternalApiAuthMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string[] PublicRoutePrefixes =
    [
        "/api/v1/calendar/health",
        "/api/openapi/",
        "/api/scalar"
    ];

    private static readonly string[] InternalBearerRoutes =
    [
        "/api/v1/calendar/notifications",
        "/api/v1/calendar/subscriptions",
        "/api/v1/calendar/users/"
    ];

    private readonly InternalApiAuthOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    public InternalApiAuthMiddleware(
        IOptions<InternalApiAuthOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        var path = request.Url.AbsolutePath;
        if (IsPublic(path) || !RequiresInternalBearer(path))
        {
            await next(context);
            return;
        }

        if (_hostEnvironment.IsDevelopment() && _options.AllowAnonymousInDevelopment)
        {
            await next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            await RejectAsync(context, request, HttpStatusCode.InternalServerError, "Internal auth token is not configured.", Errors.ServerError);
            return;
        }

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            await RejectAsync(context, request, HttpStatusCode.Unauthorized, "Missing Authorization header.", Errors.Unauthorized);
            return;
        }

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await RejectAsync(context, request, HttpStatusCode.Unauthorized, "Authorization header must use Bearer.", Errors.Unauthorized);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!string.Equals(token, _options.Token.Trim(), StringComparison.Ordinal))
        {
            await RejectAsync(context, request, HttpStatusCode.Forbidden, "Bearer token is invalid.", Errors.Unauthorized);
            return;
        }

        await next(context);
    }

    internal static bool IsPublic(string path) =>
        PublicRoutePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    internal static bool RequiresInternalBearer(string path) =>
        InternalBearerRoutes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static async Task RejectAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message,
        string code)
    {
        context.GetInvocationResult().Value = await request.ProblemAsync(statusCode, message, CancellationToken.None, code);
    }
}
