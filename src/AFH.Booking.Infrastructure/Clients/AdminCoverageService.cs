using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdminCoverageService : IAdminCoverageService
{
    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<AdminCoverageService> _logger;


    public AdminCoverageService(
    HttpClient http,
    IOptions<LocationServiceOptions> options,
    IInternalServiceAuthenticator authenticator,
    JsonSerializerOptions jsonOptions,
    ILogger<AdminCoverageService> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _jsonOptions = jsonOptions;
        _logger = logger;
    }

    public async Task<object?> GetCoverageAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/adviser-coverage");
        _authenticator.Apply(request, _options.InternalToken);

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var category = DownstreamFailureClassifier.Classify(response.StatusCode);
            return LogAndReturnNull(response.StatusCode, category);
        }

        //var envelope = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        var envelope = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(_jsonOptions, ct);

        if (envelope is null)
            return null;

        if (envelope.TryGetValue("data", out var data))
            return data;

        return envelope;
    }

    private object? LogAndReturnNull(HttpStatusCode statusCode, string category)
    {
        _logger.LogWarning(
            "Location admin coverage request failed. Status={Status} FailureCategory={FailureCategory}",
            (int)statusCode,
            category);
        return null;
    }
}
