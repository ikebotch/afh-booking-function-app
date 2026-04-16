using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AFH.Acs.Function.Middleware;
using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Common.Errors.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.Acs.Tests;

public sealed class AcsExceptionHandlingMiddlewareTests
{
    [Fact]
    public void AcsExceptionMapper_MapsJsonExceptionToBadRequest()
    {
        var mapper = new AcsExceptionMapper();
        var mapping = mapper.Map(new JsonException("Bad JSON"));

        Assert.Equal((int)HttpStatusCode.BadRequest, mapping.StatusCode);
        Assert.Equal("VALIDATION_ERROR", mapping.ErrorCode.Value);
        Assert.Single(mapping.ValidationErrors);
        Assert.Equal("body", mapping.ValidationErrors[0].Field);
    }

    [Fact]
    public void AcsExceptionMapper_MapsUnhandledExceptionsToInternalServerError()
    {
        var mapper = new AcsExceptionMapper();
        var mapping = mapper.Map(new InvalidOperationException("Unexpected failure."));

        Assert.Equal((int)HttpStatusCode.InternalServerError, mapping.StatusCode);
        Assert.Equal("INTERNAL_ERROR", mapping.ErrorCode.Value);
        Assert.Equal("An unexpected error occurred.", mapping.Message);
    }

    [Fact]
    public async Task AzureFunctionErrorResponseBuilder_UsesAcsMappingForHandledErrors()
    {
        var services = new ServiceCollection();
        services.AddAfhCommonErrorsAzureFunctions();
        var mapper = new AcsExceptionMapper();
        services.AddSingleton(mapper);
        services.AddSingleton<AFH.Common.Errors.Abstractions.IExceptionMapper>(mapper);

        var request = AcsTestHttpRequestData.Create();
        var mapping = mapper.Map(
            new JsonException("Bad JSON"),
            new ErrorContext(
                TraceId: request.FunctionContext.InvocationId,
                CorrelationId: "ctx-correlation",
                Path: request.Url.AbsolutePath,
                Method: request.Method,
                Operation: request.FunctionContext.FunctionDefinition.Name));

        var builder = services.BuildServiceProvider().GetRequiredService<AzureFunctionErrorResponseBuilder>();
        var response = await builder.BuildAsync(request, mapping);
        var payload = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"code\":\"VALIDATION_ERROR\"", payload);
        Assert.Contains("\"correlationId\":\"ctx-correlation\"", payload);
        Assert.Contains("\"validationErrors\"", payload);
    }

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        response.Body.Position = 0;
        return payload;
    }
}

internal sealed class AcsTestFunctionContext : FunctionContext
{
    private readonly Dictionary<object, object> _items = [];

    public override string InvocationId => "inv-123";

    public override string FunctionId => "func-123";

    public override TraceContext TraceContext => throw new NotSupportedException();

    public override BindingContext BindingContext => throw new NotSupportedException();

    public override RetryContext RetryContext => null!;

    public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();

    public override FunctionDefinition FunctionDefinition { get; } = new AcsTestFunctionDefinition();

    public override IDictionary<object, object> Items
    {
        get => _items;
        set
        {
            _items.Clear();

            foreach (var pair in value)
            {
                _items[pair.Key] = pair.Value;
            }
        }
    }

    public override IInvocationFeatures Features => throw new NotSupportedException();

    public override CancellationToken CancellationToken => CancellationToken.None;
}

internal sealed class AcsTestFunctionDefinition : FunctionDefinition
{
    public override string PathToAssembly => string.Empty;

    public override string EntryPoint => string.Empty;

    public override string Id => "func-123";

    public override string Name => "AcsErrorsFunction";

    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;

    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;

    public override ImmutableArray<FunctionParameter> Parameters { get; } =
        ImmutableArray<FunctionParameter>.Empty;
}

internal sealed class AcsTestHttpRequestData(FunctionContext functionContext, Uri? url = null, string method = "POST")
    : HttpRequestData(functionContext)
{
    public override Stream Body { get; } = new MemoryStream();

    public override HttpHeadersCollection Headers { get; } = [];

    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

    public override Uri Url { get; } = url ?? new Uri("https://localhost/api/v1/meet/create");

    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

    public override string Method { get; } = method;

    public override HttpResponseData CreateResponse() => new AcsTestHttpResponseData(FunctionContext);

    public static AcsTestHttpRequestData Create()
    {
        var context = new AcsTestFunctionContext();
        context.Items[CorrelationIdMiddleware.ItemKey] = "ctx-correlation";
        return new AcsTestHttpRequestData(context);
    }
}

internal sealed class AcsTestHttpCookies : HttpCookies
{
    private readonly List<IHttpCookie> _cookies = [];

    public override void Append(string name, string value) => _cookies.Add(new HttpCookie(name, value));

    public override void Append(IHttpCookie cookie) => _cookies.Add(cookie);

    public override IHttpCookie CreateNew() => new HttpCookie(string.Empty, string.Empty);
}

internal sealed class AcsTestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
{
    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; } = [];

    public override Stream Body { get; set; } = new MemoryStream();

    public override HttpCookies Cookies { get; } = new AcsTestHttpCookies();
}
