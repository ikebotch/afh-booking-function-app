using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.ApplicationInsights.Telemetry;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.AzureFunctions.Extensions;
using AFH.Common.Errors.Models;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
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
        await TryWriteErrorRecordAsync(context, mapping);
        TryTrackHandledExceptionTelemetry(context, mapping);

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
            var telemetryClient = context.InstanceServices.GetService<TelemetryClient>();
            var telemetryBuilder = context.InstanceServices.GetService<ErrorTelemetryBuilder>();
            if (telemetryClient is null || telemetryBuilder is null)
                return;

            var record = _errorRecordBuilder.Build(mapping.MappingResult);
            var telemetry = telemetryBuilder.Build(record, (properties, _) =>
            {
                properties["afh.service"] = "booking";
                properties["afh.function.name"] = context.FunctionDefinition.Name;
            });

            var eventTelemetry = new EventTelemetry(telemetry.Name)
            {
                Timestamp = telemetry.Timestamp
            };

            foreach (var pair in telemetry.Properties)
            {
                if (pair.Value is not null)
                    eventTelemetry.Properties[pair.Key] = pair.Value;
            }

            foreach (var metric in telemetry.Metrics)
                eventTelemetry.Properties[metric.Key] = metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            telemetryClient.TrackEvent(eventTelemetry);
        }
        catch (Exception telemetryEx)
        {
            _logger.LogWarning(
                telemetryEx,
                "Failed to emit handled exception telemetry. Function={FunctionName}",
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
