using AFH.Booking.Domain.Client;
using AFH.Booking.Infrastructure.Options;
//using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;


namespace AFH.Booking.Infrastructure.Clients;


public sealed class LeadsAccessToken
{
    private readonly HttpClient _http;
    private readonly LeadsOptions _opts;
    private readonly ILogger<LeadsClientDirectory> _logger;

    private string? _token;
    private DateTime _expiresUtc;

    public LeadsAccessToken(
        HttpClient http,
        IOptions<LeadsOptions> opts)
    {
        _http = http;
        _opts = opts.Value;

    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _expiresUtc)
            return _token;


        //string tokenUrl = string.Format(_opts.TokenUrl, _opts.TenantId);
        string tokenUrl = _opts.TokenUrl.Replace("{tenantId}", _opts.TenantId);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["client_secret"] = _opts.ClientSecret,
            ["scope"] = _opts.Scope,
            ["grant_type"] = "client_credentials"
        });

        var res = await _http.PostAsync(tokenUrl, content, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadFromJsonAsync<AccessTokenResponse>(ct)
                   ?? throw new InvalidOperationException("Token response empty");

        _token = json.AccessToken;
        _expiresUtc = DateTime.UtcNow.AddSeconds(json.ExpiresIn - 60);

        return _token;
    }
}
