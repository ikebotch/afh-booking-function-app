using AFH.Booking.Application.Common;
using AFH.Booking.Function.Http;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Function.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (await TryHandleHttpExceptionAsync(context, ex))
                return;

            throw;
        }
    }

    private async Task<bool> TryHandleHttpExceptionAsync(FunctionContext context, Exception ex)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
            return false;

        var mapping = MapException(ex);
        if (mapping is null)
            return false;

        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        _logger.Log(
            mapping.Level,
            ex,
            "Booking function handled exception. Function={FunctionName} FailureSource={FailureSource} FailureCode={FailureCode} DownstreamCategory={DownstreamCategory} DownstreamStatus={DownstreamStatus} Path={Path} Method={Method} CorrelationId={CorrelationId}",
            context.FunctionDefinition.Name,
            mapping.FailureSource,
            mapping.FailureCode,
            mapping.DownstreamCategory,
            mapping.DownstreamStatusCode,
            request.Url.AbsolutePath,
            request.Method,
            correlationId);

        await TryWriteFailureLogAsync(context, request, correlationId, mapping, ex);

        context.GetInvocationResult().Value = await request.ProblemAsync(
            mapping.StatusCode,
            mapping.Message,
            CancellationToken.None,
            mapping.FailureCode);

        return true;
    }

    private async Task TryWriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        string? correlationId,
        ExceptionMapping mapping,
        Exception ex)
    {
        try
        {
            var sink = context.InstanceServices.GetService<IApplicationLogSink>();
            var loggingOptions = context.InstanceServices.GetService<IOptions<ApplicationLoggingOptions>>()?.Value;
            if (sink is null || loggingOptions is null)
                return;

            await sink.WriteAsync(new ApplicationLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Level = mapping.Level == LogLevel.Error ? "Error" : "Warning",
                Category = "ExceptionHandling",
                Operation = context.FunctionDefinition.Name,
                CorrelationId = correlationId,
                ContextId = context.InvocationId,
                EventType = mapping.FailureCode,
                Result = "Failure",
                Message = mapping.Message,
                ExceptionType = ex.GetType().Name,
                ExceptionMessage = ex.Message,
                PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                {
                    FailureSource = mapping.FailureSource,
                    FailureCode = mapping.FailureCode,
                    StatusCode = (int)mapping.StatusCode,
                    Path = request.Url.AbsolutePath,
                    Method = request.Method,
                    CorrelationId = correlationId,
                    DownstreamCategory = mapping.DownstreamCategory,
                    DownstreamStatusCode = mapping.DownstreamStatusCode
                }, loggingOptions)
            }, CancellationToken.None);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(
                logEx,
                "Failed to persist handled exception log. Function={FunctionName} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                correlationId);
        }
    }

    internal static ExceptionMapping? MapException(Exception ex)
    {
        if (LooksLikeDeserializationFailure(ex))
        {
            return new ExceptionMapping(
                HttpStatusCode.BadRequest,
                "InvalidJson",
                "Request body must be valid JSON with supported date/time values.",
                "RequestDeserialization",
                null,
                null,
                LogLevel.Warning);
        }

        if (TryGetHttpStatusCode(ex, out var downstreamStatusCode))
        {
            var resolvedStatusCode = downstreamStatusCode!.Value;
            var category = ClassifyDownstreamStatus(resolvedStatusCode);
            var code = category switch
            {
                "AuthOrConfiguration" => "DependencyAuthFailed",
                "InvalidRequest" => "DependencyRejectedRequest",
                "Timeout" => "DependencyTimeout",
                _ => "DependencyUnavailable"
            };

            return new ExceptionMapping(
                category == "InvalidRequest" ? HttpStatusCode.BadGateway : HttpStatusCode.ServiceUnavailable,
                code,
                "A required downstream service could not complete the request.",
                "DownstreamDependency",
                category,
                (int)resolvedStatusCode,
                LogLevel.Warning);
        }

        if (ex is TaskCanceledException)
        {
            return new ExceptionMapping(
                HttpStatusCode.GatewayTimeout,
                "DependencyTimeout",
                "A required downstream service timed out.",
                "DownstreamDependency",
                "Timeout",
                null,
                LogLevel.Warning);
        }

        if (ex is InvalidOperationException invalidOperation &&
            invalidOperation.Message.Contains("is required", StringComparison.OrdinalIgnoreCase))
        {
            return new ExceptionMapping(
                HttpStatusCode.InternalServerError,
                "ConfigurationError",
                "A required service configuration is missing.",
                "Configuration",
                null,
                null,
                LogLevel.Error);
        }

        return null;
    }

    private static bool LooksLikeDeserializationFailure(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                if (LooksLikeDeserializationFailure(inner))
                    return true;
            }
        }

        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is JsonException || current is FormatException)
                return true;
        }

        return false;
    }

    private static bool TryGetHttpStatusCode(Exception ex, out HttpStatusCode? statusCode)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
            {
                statusCode = httpEx.StatusCode.Value;
                return true;
            }
        }

        statusCode = null;
        return false;
    }

    private static string ClassifyDownstreamStatus(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AuthOrConfiguration",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "InvalidRequest",
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => "Timeout",
            HttpStatusCode.NotFound => "NotFound",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => "Unavailable",
            _ when (int)statusCode >= 500 => "InternalFailure",
            _ => "Unavailable"
        };

    internal sealed record ExceptionMapping(
        HttpStatusCode StatusCode,
        string FailureCode,
        string Message,
        string FailureSource,
        string? DownstreamCategory,
        int? DownstreamStatusCode,
        LogLevel Level);
}
