using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Functions.Auth;
using AFH.Booking.Functions.Http;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Functions.Middleware;

public sealed class DomainUserAuthMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string[] ProtectedRoutePrefixes =
    [
        "/api/v1/me"
    ];

    private readonly IApplicationLogSink _applicationLogSink;
    private readonly ApplicationLoggingOptions _loggingOptions;

    public DomainUserAuthMiddleware(
        IApplicationLogSink applicationLogSink,
        IOptions<ApplicationLoggingOptions> loggingOptions)
    {
        _applicationLogSink = applicationLogSink;
        _loggingOptions = loggingOptions.Value;
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
        if (!ProtectedRoutePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            await WriteFailureLogAsync(context, request, HttpStatusCode.Unauthorized, Errors.Unauthorized, "Missing Authorization header.");
            context.GetInvocationResult().Value = await request.ProblemAsync(HttpStatusCode.Unauthorized, "Missing Authorization header.", CancellationToken.None, Errors.Unauthorized);
            return;
        }

        var authHeader = authHeaders.FirstOrDefault()?.Trim() ?? string.Empty;
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await WriteFailureLogAsync(context, request, HttpStatusCode.Unauthorized, Errors.Unauthorized, "Authorization header must use Bearer.");
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

            await WriteFailureLogAsync(
                context,
                request,
                statusCode,
                validation.ErrorCode ?? Errors.Unauthorized,
                validation.ErrorMessage ?? "Request failed.");

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

        return _applicationLogSink.WriteAsync(new ApplicationLogEntry
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
                FailureSource = nameof(DomainUserAuthMiddleware),
                FailureCode = failureCode,
                StatusCode = (int)statusCode,
                Path = request.Url.AbsolutePath,
                Method = request.Method,
                CorrelationId = correlationId
            }, _loggingOptions)
        }, CancellationToken.None);
    }
}
