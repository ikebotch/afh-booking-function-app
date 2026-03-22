using AFH.Booking.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace AFH.Booking.Functions.Middleware;

public sealed class OperationAuditMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationAuditMiddleware> _logger;

    public OperationAuditMiddleware(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationAuditMiddleware> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

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
            var statusCode = (int)(response?.StatusCode ?? (unhandled is null ? HttpStatusCode.OK : HttpStatusCode.InternalServerError));
            var correlationId = TryGetString(context, CorrelationIdMiddleware.ItemKey);
            var operationId = context.InvocationId;
            var functionName = context.FunctionDefinition.Name;
            var method = req.Method;
            var path = req.Url.AbsolutePath;
            var query = req.Url.Query;
            var errorType = unhandled?.GetType().Name;
            var errorMessage = unhandled?.Message;
            var durationMs = sw.ElapsedMilliseconds;
            var createdUtc = DateTime.UtcNow;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO dbo.IntegrationOperationAudit
                    (
                        Id,
                        ServiceName,
                        FunctionName,
                        Method,
                        Path,
                        QueryString,
                        CorrelationId,
                        OperationId,
                        StatusCode,
                        DurationMs,
                        ErrorType,
                        ErrorMessage,
                        CreatedUtc
                    )
                    VALUES
                    (
                        {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}
                    )
                    """,
                    Guid.NewGuid().ToString("N"),
                    "booking",
                    functionName,
                    method,
                    path,
                    query,
                    correlationId ?? string.Empty,
                    operationId,
                    statusCode,
                    durationMs,
                    errorType,
                    errorMessage,
                    createdUtc);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist integration operation audit. Function={FunctionName} CorrelationId={CorrelationId}",
                    functionName,
                    correlationId);
            }
        }
    }

    private static string? TryGetString(FunctionContext context, string key)
    {
        if (!context.Items.TryGetValue(key, out var value))
            return null;

        return value?.ToString();
    }
}
