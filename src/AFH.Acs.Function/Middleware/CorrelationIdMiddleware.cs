using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace AFH.Acs.Function.Middleware;

public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    public const string HeaderName = "x-correlation-id";
    public const string ItemKey = "correlation-id";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            await next(context);
            return;
        }

        var correlationId = req.Headers.TryGetValues(HeaderName, out var values)
            ? values.FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        await next(context);

        var response = context.GetInvocationResult().Value as HttpResponseData;
        if (response is not null && !response.Headers.TryGetValues(HeaderName, out _))
            response.Headers.Add(HeaderName, correlationId);
    }
}
