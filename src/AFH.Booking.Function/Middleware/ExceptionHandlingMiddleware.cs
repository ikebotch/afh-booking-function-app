using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.AzureFunctions.Extensions;
using AFH.Common.Errors.AzureFunctions.Mapping;
using AFH.Common.Errors.Mapping;
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
    private readonly ErrorRecordBuilder _errorRecordBuilder = new();
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly BookingExceptionMapper _exceptionMapper;
    private readonly ErrorResponseBuilder _errorResponseBuilder;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        BookingExceptionMapper exceptionMapper,
        ErrorResponseBuilder errorResponseBuilder)
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
        await TryWriteErrorRecordAsync(context, mapping);
        TryTrackHandledExceptionTelemetry(context, mapping);
        await TrySendHandledExceptionEmailAsync(context, mapping);

        context.GetInvocationResult().Value = await BuildHandledResponseAsync(
            request,
            mapping.MappingResult,
            CancellationToken.None);

        return true;
    }

    internal async Task<HttpResponseData> BuildHandledResponseAsync(
        HttpRequestData request,
        ExceptionMappingResult mappingResult,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCodeResolver.Resolve(mappingResult));
        var errorResponse = _errorResponseBuilder.Build(mappingResult);
        await response.WriteAsJsonAsync(errorResponse, cancellationToken: cancellationToken);
        return response;
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

    private async Task TryWriteErrorRecordAsync(
        FunctionContext context,
        BookingExceptionMapper.BookingHandledException mapping)
    {
        try
        {
            var writer = context.InstanceServices.GetService<IErrorPersistenceWriter>();
            if (writer is null)
                return;

            var record = _errorRecordBuilder.Build(mapping.MappingResult);
            await writer.WriteAsync(record, CancellationToken.None);
        }
        catch (Exception persistenceEx)
        {
            _logger.LogWarning(
                persistenceEx,
                "Failed to persist handled exception error record. Function={FunctionName}",
                context.FunctionDefinition.Name);
        }
    }

    private void TryTrackHandledExceptionTelemetry(
        FunctionContext context,
        BookingExceptionMapper.BookingHandledException mapping)
    {
        try
        {
            var emitter = context.InstanceServices.GetService<BookingHandledErrorTelemetryEmitter>();
            if (emitter is null)
                return;

            var record = _errorRecordBuilder.Build(mapping.MappingResult);
            emitter.Track(record, context.FunctionDefinition.Name);
        }
        catch (Exception telemetryEx)
        {
            _logger.LogWarning(
                telemetryEx,
                "Failed to emit handled exception telemetry. Function={FunctionName}",
                context.FunctionDefinition.Name);
        }
    }

    private async Task TrySendHandledExceptionEmailAsync(
        FunctionContext context,
        BookingExceptionMapper.BookingHandledException mapping)
    {
        if (!BookingHandledErrorEmailPolicy.ShouldNotify(mapping.MappingResult))
            return;

        try
        {
            var notifier = context.InstanceServices.GetService<IErrorNotifier>();
            if (notifier is null)
                return;

            var record = _errorRecordBuilder.Build(mapping.MappingResult);
            var request = BookingHandledErrorEmailPolicy.CreateNotificationRequest(
                context.FunctionDefinition.Name,
                mapping.MappingResult.StatusCode,
                record);

            await notifier.NotifyAsync(request, CancellationToken.None);
        }
        catch (Exception emailEx)
        {
            _logger.LogWarning(
                emailEx,
                "Failed to send handled exception email notification. Function={FunctionName}",
                context.FunctionDefinition.Name);
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
