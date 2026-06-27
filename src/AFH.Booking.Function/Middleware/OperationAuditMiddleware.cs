using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AFH.Booking.Function.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(ILogger<OperationAuditMiddleware> logger)
    {
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
            var userProfileId = Header(req, "x-afh-user-profile-id");

            try
            {
                var sink = context.InstanceServices.GetService<IApplicationLogSink>();
                var loggingOptions = context.InstanceServices.GetService<IOptions<ApplicationLoggingOptions>>()?.Value;
                if (sink is not null && loggingOptions is not null)
                {
                    await sink.WriteAsync(new ApplicationLogEntry
                    {
                        OccurredUtc = DateTime.UtcNow,
                        Level = GetLevel(unhandled, statusCode),
                        Category = "FunctionInvocation",
                        Operation = context.FunctionDefinition.Name,
                        CorrelationId = correlationId,
                        UserId = userProfileId,
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
                            DurationMs = sw.ElapsedMilliseconds,
                            AuthorizedPermission = Header(req, "x-afh-authorized-permission"),
                            Actor = new
                            {
                                UserProfileId = userProfileId,
                                ExternalSubject = Header(req, "x-afh-user-external-subject"),
                                Email = Header(req, "x-afh-user-email"),
                                DisplayName = Header(req, "x-afh-user-display-name"),
                                AdviserId = Header(req, "x-afh-user-adviser-id")
                            }
                        }, loggingOptions)
                    }, CancellationToken.None);
                }
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

    private static string? Header(HttpRequestData? req, string name)
        => req is not null && req.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
}
