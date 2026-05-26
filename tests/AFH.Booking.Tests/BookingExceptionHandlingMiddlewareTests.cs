using AFH.Booking.Function.Middleware;
using AFH.Common.Errors.AzureFunctions.Builders;
using AFH.Common.Errors.AzureFunctions.DependencyInjection;
using AFH.Common.Errors.AzureFunctions.Extensions;
using AFH.Common.Errors.Codes;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AFH.Booking.Tests;

public class BookingExceptionHandlingMiddlewareTests
{
    [Fact]
    public void MapException_MapsJsonExceptionToBadRequest()
    {
        var mapping = new BookingExceptionMapper().TryMap(new JsonException("Bad JSON"));

        Assert.NotNull(mapping);
        Assert.Equal((int)HttpStatusCode.BadRequest, mapping!.MappingResult.StatusCode);
        Assert.Equal("InvalidJson", mapping.MappingResult.ErrorCode.Value);
        Assert.Equal("RequestDeserialization", mapping.FailureSource);
        Assert.Single(mapping.MappingResult.ValidationErrors);
        Assert.Equal("body", mapping.MappingResult.ValidationErrors[0].Field);
        Assert.Equal(ValidationErrorCodes.InvalidFormat.Value, mapping.MappingResult.ValidationErrors[0].Code);
    }

    [Fact]
    public void MapException_MapsDownstreamUnauthorizedToDependencyAuthFailed()
    {
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        var mapping = new BookingExceptionMapper().TryMap(ex);

        Assert.NotNull(mapping);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, mapping!.MappingResult.StatusCode);
        Assert.Equal("DependencyAuthFailed", mapping.MappingResult.ErrorCode.Value);
        Assert.Equal("DownstreamDependency", mapping.FailureSource);
        Assert.Equal("AuthOrConfiguration", mapping.DownstreamCategory);
        Assert.Equal(401, mapping.DownstreamStatusCode);
    }

    [Fact]
    public async Task AzureFunctionErrorResponseBuilder_UsesBookingMapperForHandledJsonErrors()
    {
        var services = new ServiceCollection();
        services.AddAfhCommonErrorsAzureFunctions();
        var mapper = new BookingExceptionMapper();
        services.AddSingleton(mapper);
        services.AddSingleton<AFH.Common.Errors.Abstractions.IExceptionMapper>(mapper);

        var builder = services.BuildServiceProvider().GetRequiredService<AzureFunctionErrorResponseBuilder>();
        var request = TestHttpRequestData.Create();
        var context = request.FunctionContext;

        var response = await builder.BuildAsync(context, request, new JsonException("Bad JSON"));
        var payload = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"code\":\"InvalidJson\"", payload);
        Assert.Contains("\"correlationId\":\"ctx-correlation\"", payload);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_BuildHandledResponseAsync_ReturnsCleanJsonForDownstreamNotFound()
    {
        var middleware = new ExceptionHandlingMiddleware(
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new BookingExceptionMapper(),
            new AFH.Common.Errors.Builders.ErrorResponseBuilder(new BookingExceptionMapper()));

        var request = TestHttpRequestData.Create();
        var mapping = new BookingExceptionMapper().Map(
            new HttpRequestException("ACS meeting link request failed.", null, HttpStatusCode.NotFound),
            request.ToErrorContext());

        var response = await middleware.BuildHandledResponseAsync(request, mapping, CancellationToken.None);
        var payload = await ReadBodyAsync(response);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("DependencyUnavailable", root.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal("ctx-correlation", root.GetProperty("CorrelationId").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_BuildHandledResponseAsync_WritesJsonToNonSeekableResponseBody()
    {
        var middleware = new ExceptionHandlingMiddleware(
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            new BookingExceptionMapper(),
            new AFH.Common.Errors.Builders.ErrorResponseBuilder(new BookingExceptionMapper()));

        var request = NonSeekableTestHttpRequestData.Create();
        var mapping = new BookingExceptionMapper().Map(
            new HttpRequestException("ACS meeting link request failed.", null, HttpStatusCode.NotFound),
            request.ToErrorContext());

        var response = await middleware.BuildHandledResponseAsync(request, mapping, CancellationToken.None);
        var nonSeekableResponse = Assert.IsType<NonSeekableTestHttpResponseData>(response);
        var payload = nonSeekableResponse.GetBodyText();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("DependencyUnavailable", root.GetProperty("Error").GetProperty("Code").GetString());
        Assert.Equal("ctx-correlation", root.GetProperty("CorrelationId").GetString());
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

internal sealed class TestFunctionContext : FunctionContext
{
    private readonly Dictionary<object, object> _items = [];

    public override string InvocationId => "inv-123";

    public override string FunctionId => "func-123";

    public override TraceContext TraceContext => throw new NotSupportedException();

    public override BindingContext BindingContext => throw new NotSupportedException();

    public override RetryContext RetryContext => null!;

    public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();

    public override FunctionDefinition FunctionDefinition { get; } = new TestFunctionDefinition();

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

internal sealed class TestFunctionDefinition : FunctionDefinition
{
    public override string PathToAssembly => string.Empty;

    public override string EntryPoint => string.Empty;

    public override string Id => "func-123";

    public override string Name => "BookingErrorsFunction";

    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;

    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;

    public override ImmutableArray<FunctionParameter> Parameters { get; } =
        ImmutableArray<FunctionParameter>.Empty;
}

internal sealed class TestHttpRequestData : HttpRequestData
{
    public TestHttpRequestData(FunctionContext functionContext, Uri? url = null, string method = "GET")
        : base(functionContext)
    {
        Url = url ?? new Uri("https://localhost/api/v1/bookings");
        Method = method;
    }

    public override Stream Body { get; } = new MemoryStream();

    public override HttpHeadersCollection Headers { get; } = [];

    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

    public override Uri Url { get; }

    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(FunctionContext);
    }

    public static TestHttpRequestData Create(Uri? url = null, string method = "GET")
    {
        var context = new TestFunctionContext();
        ConfigureSerializer(context);
        context.Items[CorrelationIdMiddleware.ItemKey] = "ctx-correlation";
        context.Items["CorrelationId"] = "ctx-correlation";
        return new TestHttpRequestData(context, url, method);
    }

    private static void ConfigureSerializer(TestFunctionContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<WorkerOptions>>(Options.Create(new WorkerOptions
        {
            Serializer = new JsonObjectSerializer()
        }));

        context.InstanceServices = services.BuildServiceProvider();
    }
}

internal sealed class TestHttpCookies : HttpCookies
{
    private readonly List<IHttpCookie> _cookies = [];

    public override void Append(string name, string value)
    {
        _cookies.Add(new HttpCookie(name, value));
    }

    public override void Append(IHttpCookie cookie)
    {
        _cookies.Add(cookie);
    }

    public override IHttpCookie CreateNew()
    {
        return new HttpCookie(string.Empty, string.Empty);
    }
}

internal sealed class TestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
{
    public override HttpStatusCode StatusCode { get; set; }

    public override HttpHeadersCollection Headers { get; set; } = [];

    public override Stream Body { get; set; } = new MemoryStream();

    public override HttpCookies Cookies { get; } = new TestHttpCookies();
}

internal sealed class NonSeekableTestHttpRequestData(FunctionContext functionContext, Uri? url = null, string method = "GET")
    : HttpRequestData(functionContext)
{
    public override Stream Body { get; } = new MemoryStream();
    public override HttpHeadersCollection Headers { get; } = [];
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];
    public override Uri Url { get; } = url ?? new Uri("https://localhost/api/v1/bookings/holds/hold-1/confirm");
    public override IEnumerable<ClaimsIdentity> Identities { get; } = [];
    public override string Method { get; } = method;

    public override HttpResponseData CreateResponse() => LastResponse = new NonSeekableTestHttpResponseData(FunctionContext);

    public NonSeekableTestHttpResponseData? LastResponse { get; private set; }

    public static NonSeekableTestHttpRequestData Create()
    {
        var context = new TestFunctionContext();
        ConfigureSerializer(context);
        context.Items[CorrelationIdMiddleware.ItemKey] = "ctx-correlation";
        context.Items["CorrelationId"] = "ctx-correlation";
        return new NonSeekableTestHttpRequestData(context, method: "POST");
    }

    private static void ConfigureSerializer(TestFunctionContext context)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<WorkerOptions>>(Options.Create(new WorkerOptions
        {
            Serializer = new JsonObjectSerializer()
        }));

        context.InstanceServices = services.BuildServiceProvider();
    }
}

internal sealed class NonSeekableTestHttpResponseData(FunctionContext functionContext) : HttpResponseData(functionContext)
{
    private readonly NonSeekableCaptureStream _body = new();

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; } = [];
    public override Stream Body
    {
        get => _body;
        set => throw new NotSupportedException();
    }

    public override HttpCookies Cookies { get; } = new TestHttpCookies();

    public string GetBodyText() => _body.GetContent();
}

internal sealed class NonSeekableCaptureStream : Stream
{
    private readonly MemoryStream _inner = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer, offset, count, cancellationToken);

    public string GetContent() => Encoding.UTF8.GetString(_inner.ToArray());
}
