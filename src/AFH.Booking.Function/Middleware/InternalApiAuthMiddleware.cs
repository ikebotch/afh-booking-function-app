using AFH.Booking.Domain.Options;
using AFH.Booking.Function.Http;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Middleware;

public sealed class InternalApiAuthMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string[] PublicRoutePrefixes =
    [
        "/api/v1/calendar/health",
        "/api/openapi/",
        "/api/scalar"
    ];

    private static readonly string[] NonInternalBearerRoutePrefixes =
    [
        "/api/v1/me",
        "/api/v1/self-service/"
    ];

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
            await WriteFailureLogAsync(context, request, HttpStatusCode.InternalServerError, Errors.ServerError, "Internal auth token is not configured.");
            await RejectAsync(context, request, HttpStatusCode.InternalServerError, "Internal auth token is not configured.", Errors.ServerError);
            return;
        }

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            await WriteFailureLogAsync(context, request, HttpStatusCode.Unauthorized, Errors.Unauthorized, "Missing Authorization header.");
            await RejectAsync(context, request, HttpStatusCode.Unauthorized, "Missing Authorization header.", Errors.Unauthorized);
            return;
        }

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await WriteFailureLogAsync(context, request, HttpStatusCode.Unauthorized, Errors.Unauthorized, "Authorization header must use Bearer.");
            await RejectAsync(context, request, HttpStatusCode.Unauthorized, "Authorization header must use Bearer.", Errors.Unauthorized);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!string.Equals(token, _options.Token.Trim(), StringComparison.Ordinal))
        {
            await WriteFailureLogAsync(context, request, HttpStatusCode.Forbidden, Errors.Unauthorized, "Bearer token is invalid.");
            await RejectAsync(context, request, HttpStatusCode.Forbidden, "Bearer token is invalid.", Errors.Unauthorized);
            return;
        }

        await next(context);
    }

    public static bool IsPublic(string path) =>
        PublicRoutePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static bool RequiresInternalBearer(string path) =>
        !NonInternalBearerRoutePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static async Task RejectAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string message,
        string code)
    {
        context.GetInvocationResult().Value = await request.ProblemAsync(statusCode, message, CancellationToken.None, code);
    }

    private Task WriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string failureCode,
        string detail)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        try
        {
            var sink = context.InstanceServices.GetService<IApplicationLogSink>();
            var loggingOptions = context.InstanceServices.GetService<IOptions<ApplicationLoggingOptions>>()?.Value;
            if (sink is null || loggingOptions is null)
                return Task.CompletedTask;

            return sink.WriteAsync(new ApplicationLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Level = statusCode == HttpStatusCode.InternalServerError ? "Error" : "Warning",
                Category = "Authorization",
                Operation = context.FunctionDefinition.Name,
                CorrelationId = correlationId,
                ContextId = context.InvocationId,
                EventType = failureCode,
                Result = "Failure",
                Message = detail,
                PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                {
                    FailureSource = nameof(InternalApiAuthMiddleware),
                    FailureCode = failureCode,
                    StatusCode = (int)statusCode,
                    Path = request.Url.AbsolutePath,
                    Method = request.Method,
                    CorrelationId = correlationId
                }, loggingOptions)
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist internal auth failure log. Function={FunctionName} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                correlationId);
            return Task.CompletedTask;
        }
    }
}
