using AFH.Acs.Domain;
using AFH.Acs.Function.Http;
using AFH.Acs.Function.Options;
using AFH.Acs.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace AFH.Acs.Function.Middleware;

public sealed class InternalApiAuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly InternalApiAuthOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<InternalApiAuthMiddleware> _logger;

    public InternalApiAuthMiddleware(
        IOptions<InternalApiAuthOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<InternalApiAuthMiddleware> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        var policy = EndpointAccessPolicies.GetPolicy(context.FunctionDefinition.Name);
        if (policy is not EndpointAccessPolicy.InternalOnly)
        {
            await next(context);
            return;
        }

        if (_hostEnvironment.IsDevelopment() && _options.AllowAnonymousInDevelopment)
        {
            await next(context);
            return;
        }

        var authorizationHeader = request.Headers.TryGetValues("Authorization", out var authHeaders)
            ? authHeaders.FirstOrDefault()
            : null;
        var failure = ValidateAuthorization(_options.Token, authorizationHeader);
        if (failure is not null)
        {
            var (statusCode, message, code) = failure.Value;
            await RejectAsync(context, request, statusCode, message, code);
            return;
        }

        await next(context);
    }

    private static async Task RejectAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message,
        string code)
    {
        context.GetInvocationResult().Value = await request.ProblemAsync(statusCode, message, CancellationToken.None, code);
    }

    public static (HttpStatusCode StatusCode, string Message, string Code)? ValidateAuthorization(
        string? expectedToken,
        string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return (HttpStatusCode.InternalServerError, "Internal auth token is not configured.", "SERVER_ERROR");
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return (HttpStatusCode.Unauthorized, "Missing Authorization header.", "UNAUTHORIZED");
        }

        var header = authorizationHeader.Trim();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpStatusCode.Unauthorized, "Authorization header must use Bearer.", "UNAUTHORIZED");
        }

        var token = header["Bearer ".Length..].Trim();
        if (!string.Equals(token, expectedToken.Trim(), StringComparison.Ordinal))
        {
            return (HttpStatusCode.Forbidden, "Bearer token is invalid.", "UNAUTHORIZED");
        }

        return null;
    }
}
