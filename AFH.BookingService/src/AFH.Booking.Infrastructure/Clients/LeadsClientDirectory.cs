using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Options;
using Common.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class LeadsClientDirectory : IClientDirectory
{
    private readonly HttpClient _http;
    private readonly LeadsOptions _opts;
    private readonly LeadsAccessToken _tokenClient;
    private readonly ILogger<LeadsClientDirectory> _logger;

    public LeadsClientDirectory(
        HttpClient http,
        IOptions<LeadsOptions> opts,
        ILogger<LeadsClientDirectory> logger,
        LeadsAccessToken tokenClient)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger;
        _tokenClient = tokenClient;
    }



    public async Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct)
    {

        if (string.IsNullOrWhiteSpace(transactionIdOrClientId))
            throw new ArgumentException("transactionIdOrClientId is required.", nameof(transactionIdOrClientId));

        if (string.IsNullOrWhiteSpace(_opts.FunctionKey))
            throw new InvalidOperationException("Leads:FunctionKey is missing.");

        if (string.IsNullOrWhiteSpace(_opts.BaseUrl))
            throw new InvalidOperationException("Leads:FunctionKey is missing.");


        var token = await _tokenClient.GetAccessTokenAsync(ct);


        var tx = Uri.EscapeDataString(transactionIdOrClientId.Trim());
        var code = Uri.EscapeDataString(_opts.FunctionKey.Trim());


        var placeholders = new Dictionary<string, string>
                {
                    { "baseUrl", _opts.BaseUrl },
                    { "functionKey", code },
                    { "transactionId", tx }
                };

        string prospectsUrl = UrlTemplateHelper.Build(_opts.ProspectsUrl, placeholders);


        var req = new HttpRequestMessage(HttpMethod.Get, prospectsUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);


        _logger.LogInformation("AFH Integration lookup: {Url}", prospectsUrl);

        using var res = await _http.SendAsync(req, ct);




        if (res.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("AFH Integration rejected request. Status={Status} Body={Body}", (int)res.StatusCode, body);
            throw new InvalidOperationException("AFH Integration rejected the request (check function key).");
        }

        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<ClientDirectoryItem>(cancellationToken: ct);
        return payload;
    }
}