using AFH.Common.Errors.Abstractions;
using AFH.Common.Errors.Builders;
using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AFH.Acs.Function.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ErrorRecordBuilder _errorRecordBuilder = new();
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly AcsExceptionMapper _exceptionMapper;
    private readonly AzureFunctionErrorResponseBuilder _errorResponseBuilder;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        AcsExceptionMapper exceptionMapper,
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
            var request = await context.GetHttpRequestDataAsync();
            if (request is null)
            {
                throw;
            }

            var mapping = _exceptionMapper.Map(ex, CreateErrorContext(context, request));
            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                ? value?.ToString()
                : null;

            _logger.Log(
                mapping.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
                ex,
                "ACS function handled exception. Function={FunctionName} FailureCode={FailureCode} Path={Path} Method={Method} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                mapping.ErrorCode.Value,
                request.Url.AbsolutePath,
                request.Method,
                correlationId);

            await TrySendHandledExceptionEmailAsync(context, mapping);

            context.GetInvocationResult().Value = await _errorResponseBuilder.BuildAsync(
                request,
                mapping,
                CancellationToken.None);
        }
    }

    private async Task TrySendHandledExceptionEmailAsync(FunctionContext context, ExceptionMappingResult mapping)
    {
        if (!AcsHandledErrorEmailPolicy.ShouldNotify(mapping))
            return;

        try
        {
            var notifier = context.InstanceServices.GetService<IErrorNotifier>();
            if (notifier is null)
                return;

            var record = _errorRecordBuilder.Build(mapping);
            var request = AcsHandledErrorEmailPolicy.CreateNotificationRequest(
                context.FunctionDefinition.Name,
                mapping.StatusCode,
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
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        return new ErrorContext(
            TraceId: context.InvocationId,
            CorrelationId: correlationId,
            Path: request.Url.AbsolutePath,
            Method: request.Method,
            Operation: context.FunctionDefinition.Name,
            Metadata: new Dictionary<string, string?>
            {
                ["functionId"] = context.FunctionId,
                ["invocationId"] = context.InvocationId,
                ["host"] = request.Url.Host
            });
    }
}
