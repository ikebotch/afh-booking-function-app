using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class BookingIdentityAdminClient : IBookingIdentityAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<BookingIdentityAdminClient> _logger;

    public BookingIdentityAdminClient(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<BookingIdentityAdminClient> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _http.SendAsync(request, ct);
        return await ReadResponseAsync<T>(response, ct);
    }

    public async Task<T?> PostAsync<TRequest, T>(string path, TRequest body, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await _http.SendAsync(request, ct);
        return await ReadResponseAsync<T>(response, ct);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Delete, path);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        if (!response.IsSuccessStatusCode)
        {
            LogFailure(response);
            return false;
        }

        return true;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        var request = new HttpRequestMessage(
            method,
            $"{_options.BaseUrl.TrimEnd('/')}/api/internal/identity/v1/{path.TrimStart('/')}");

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(request, _options.InternalToken);
        return request;
    }

    private async Task<T?> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        if (!response.IsSuccessStatusCode)
        {
            LogFailure(response);
            return default;
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions, ct);
        return envelope is { Success: true } ? envelope.Data : default;
    }

    private void LogFailure(HttpResponseMessage response)
    {
        _logger.LogWarning(
            "Identity admin request failed. Status={Status} FailureCategory={FailureCategory}",
            (int)response.StatusCode,
            DownstreamFailureClassifier.Classify(response.StatusCode));
    }

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
