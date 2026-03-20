using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class AdminCoverageService : IAdminCoverageService
{
    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;

    public AdminCoverageService(HttpClient http, IOptions<LocationServiceOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<object?> GetCoverageAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return null;

        var url = "/api/v1/admin/adviser-coverage";
        if (!string.IsNullOrWhiteSpace(_options.MasterKey))
            url += $"?code={Uri.EscapeDataString(_options.MasterKey)}";

        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var envelope = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        if (envelope is null)
            return null;

        if (envelope.TryGetValue("data", out var data))
            return data;

        return envelope;
    }
}
