using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace AFH.Acs.Function.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(
        ILogger<OperationAuditMiddleware> logger)
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

            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                ? value?.ToString()
                : null;

            _logger.LogInformation(
                "operation_audit service={Service} function={Function} method={Method} path={Path} status={StatusCode} durationMs={DurationMs} correlationId={CorrelationId} operationId={OperationId} errorType={ErrorType}",
                "acs",
                context.FunctionDefinition.Name,
                req?.Method ?? "FUNCTION",
                req?.Url.AbsolutePath ?? context.FunctionDefinition.Name,
                statusCode,
                sw.ElapsedMilliseconds,
                correlationId,
                context.InvocationId,
                unhandled?.GetType().Name);
        }
    }
}
