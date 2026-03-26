using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;

namespace AFH.Booking.Functions.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IApplicationLogSink _applicationLogSink;
    private readonly ApplicationLoggingOptions _loggingOptions;
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(
        IApplicationLogSink applicationLogSink,
        IOptions<ApplicationLoggingOptions> loggingOptions,
        ILogger<OperationAuditMiddleware> logger)
    {
        _applicationLogSink = applicationLogSink;
        _loggingOptions = loggingOptions.Value;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        var sw = Stopwatch.StartNew();
        Exception? unhandled = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            unhandled = ex;
            throw;
        }
        finally
        {
            sw.Stop();
            var response = context.GetInvocationResult().Value as HttpResponseData;
            var statusCode = response is null
                ? (unhandled is null ? (int?)null : (int)HttpStatusCode.InternalServerError)
                : (int)response.StatusCode;
            var correlationId = TryGetString(context, CorrelationIdMiddleware.ItemKey);

            try
            {
                await _applicationLogSink.WriteAsync(new ApplicationLogEntry
                {
                    OccurredUtc = DateTime.UtcNow,
                    Level = GetLevel(unhandled, statusCode),
                    Category = "FunctionInvocation",
                    Operation = context.FunctionDefinition.Name,
                    CorrelationId = correlationId,
                    ContextId = context.InvocationId,
                    EventType = unhandled is null ? "InvocationCompleted" : "InvocationFailed",
                    Result = unhandled is null && (statusCode is null || statusCode < 400) ? "Success" : "Failure",
                    Message = unhandled is null
                        ? "Booking function invocation completed."
                        : "Booking function invocation failed.",
                    ExceptionType = unhandled?.GetType().Name,
                    ExceptionMessage = unhandled?.Message,
                    PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                    {
                        Trigger = req is null ? "Function" : "Http",
                        Method = req?.Method,
                        Path = req?.Url.AbsolutePath,
                        StatusCode = statusCode,
                        DurationMs = sw.ElapsedMilliseconds
                    }, _loggingOptions)
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist booking application log. Function={FunctionName} CorrelationId={CorrelationId}",
                    context.FunctionDefinition.Name,
                    correlationId);
            }
        }
    }

    private static string GetLevel(Exception? unhandled, int? statusCode)
    {
        if (unhandled is not null || statusCode >= 500)
            return "Error";

        if (statusCode >= 400)
            return "Warning";

        return "Information";
    }

    private static string? TryGetString(FunctionContext context, string key)
    {
        if (!context.Items.TryGetValue(key, out var value))
            return null;

        return value?.ToString();
    }
}
