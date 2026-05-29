using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Infrastructure.Clients;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Auth;

public sealed class AdviserUserContextClient : IAdviserUserContextClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly ILogger<AdviserUserContextClient> _logger;

    public AdviserUserContextClient(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        ILogger<AdviserUserContextClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdviserUserContext?> GetCurrentUserAsync(string bearerToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Adviser user context request failed. Status={Status} FailureCategory={FailureCategory}",
                (int)response.StatusCode,
                DownstreamFailureClassifier.Classify(response.StatusCode));
            return null;
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AdviserUserContext>>(JsonOptions, ct);
        return envelope?.Data;
    }

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
