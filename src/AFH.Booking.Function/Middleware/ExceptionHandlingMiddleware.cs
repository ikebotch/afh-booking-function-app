using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.AzureFunctions.Extensions;
using AFH.Common.Errors.Models;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Function.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly BookingExceptionMapper _exceptionMapper;
    private readonly AzureFunctionErrorResponseBuilder _errorResponseBuilder;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        BookingExceptionMapper exceptionMapper,
        AzureFunctionErrorResponseBuilder errorResponseBuilder)
    {
        _logger = logger;
        _exceptionMapper = exceptionMapper;
        _errorResponseBuilder = errorResponseBuilder;
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

        var errorContext = CreateErrorContext(context, request);
        var mapping = _exceptionMapper.TryMap(ex, errorContext);
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
            mapping.MappingResult.ErrorCode.Value,
            mapping.DownstreamCategory,
            mapping.DownstreamStatusCode,
            request.Url.AbsolutePath,
            request.Method,
            correlationId);

        await TryWriteFailureLogAsync(context, request, correlationId, mapping, ex);

        context.GetInvocationResult().Value = await _errorResponseBuilder.BuildAsync(
            request,
            mapping.MappingResult,
            CancellationToken.None);

        return true;
    }

    private async Task TryWriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        string? correlationId,
        BookingExceptionMapper.BookingHandledException mapping,
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
                EventType = mapping.MappingResult.ErrorCode.Value,
                Result = "Failure",
                Message = mapping.MappingResult.Message,
                ExceptionType = ex.GetType().Name,
                ExceptionMessage = ex.Message,
                PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                {
                    FailureSource = mapping.FailureSource,
                    FailureCode = mapping.MappingResult.ErrorCode.Value,
                    StatusCode = mapping.MappingResult.StatusCode,
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

    private static ErrorContext CreateErrorContext(FunctionContext context, HttpRequestData request)
    {
        var requestContext = request.ToErrorContext();
        var functionContext = context.ToErrorContext();

        return new ErrorContext(
            TraceId: requestContext.TraceId ?? functionContext.TraceId,
            CorrelationId: requestContext.CorrelationId ?? functionContext.CorrelationId,
            Path: requestContext.Path,
            Method: requestContext.Method,
            Operation: functionContext.Operation,
            UserId: functionContext.UserId,
            Metadata: requestContext.Metadata ?? functionContext.Metadata);
    }
}
