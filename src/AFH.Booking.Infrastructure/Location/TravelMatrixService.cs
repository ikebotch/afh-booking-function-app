using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using AFH.Booking.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Location;

public sealed class TravelMatrixService : ITravelMatrixService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _opt;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<TravelMatrixService> _logger;

    public TravelMatrixService(
        HttpClient http,
        IOptions<LocationServiceOptions> opt,
        IInternalServiceAuthenticator authenticator,
        ILogger<TravelMatrixService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<TravelMatrixResult> GetAsync(TravelMatrixRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_opt.BaseUrl.TrimEnd('/')}/api/v1/location/inperson/advisers/search")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        if (!string.IsNullOrWhiteSpace(_opt.FunctionKey))
            httpRequest.Headers.TryAddWithoutValidation("x-functions-key", _opt.FunctionKey.Trim());
        _authenticator.Apply(httpRequest, _opt.InternalToken);

        using var resp = await _http.SendAsync(httpRequest, ct);

        if (resp.IsSuccessStatusCode)
        {
            var data = await ReadEnvelopedOrRawAsync<LocationAdviserSearchResponse>(resp, ct)
                       ?? new LocationAdviserSearchResponse();


            return MapBack(request, data);
        }

        var body = await resp.Content.ReadAsStringAsync(ct);


        _logger.LogWarning("Location service call failed. Status={Status} Body={Body}", (int)resp.StatusCode, body);

     

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Location service rejected the request (check internal bearer configuration).");

        return new TravelMatrixResult(); // degrade gracefully
    }


    private static TravelMatrixResult MapBack(TravelMatrixRequest original, LocationAdviserSearchResponse resp)
    {
        var result = new TravelMatrixResult();

        result.Candidates = resp.Candidates.Select(c => new LocationCandidate
        {
            AdviserId = c.AdviserId,
            MailboxUserId = c.MailboxUserId,
            GoldStar = c.GoldStar,
            TravelMinutes = c.TravelToClient.EtaMinutes,
            DistanceMiles = c.TravelToClient.DistanceMiles,
            CompanyBufferMinutes = c.Buffers?.CompanyBufferMinutes
        }).ToList();

        return result;
    }

    private static async Task<T?> ReadEnvelopedOrRawAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            var enveloped = JsonSerializer.Deserialize<ApiEnvelope<T>>(json, JsonOptions);
            if (enveloped?.Data is not null)
                return enveloped.Data;

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
