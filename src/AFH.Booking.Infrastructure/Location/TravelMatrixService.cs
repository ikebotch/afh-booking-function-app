using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
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
    private readonly ILogger<TravelMatrixService> _logger;

    public TravelMatrixService(
        HttpClient http,
        IOptions<LocationServiceOptions> opt,
        ILogger<TravelMatrixService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    public async Task<TravelMatrixResult> GetAsync(TravelMatrixRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.MasterKey))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:MasterKey is required.");

        // Map your domain request -> location service request
        //var payload = Map(request);

        var url = $"{_opt.BaseUrl}/api/v1/location/inperson/advisers/search?code={Uri.EscapeDataString(_opt.MasterKey)}";

        using var resp = await _http.PostAsJsonAsync(url, request, JsonOptions, ct);

        if (resp.IsSuccessStatusCode)
        {
            var data = await ReadEnvelopedOrRawAsync<LocationAdviserSearchResponse>(resp, ct)
                       ?? new LocationAdviserSearchResponse();


            return MapBack(request, data);
        }

        var body = await resp.Content.ReadAsStringAsync(ct);


        _logger.LogWarning("Location service call failed. Status={Status} Body={Body}", (int)resp.StatusCode, body);

     

        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Location service rejected the request (check master key).");

        return new TravelMatrixResult(); // degrade gracefully
    }


    private static TravelMatrixResult MapBack(TravelMatrixRequest original, LocationAdviserSearchResponse resp)
    {
        var result = new TravelMatrixResult();

        result.Candidates = resp.Candidates.Select(c => new LocationCandidate
        {
            AdviserId = c.AdviserId,
            GoldStar = c.GoldStar,
            TravelMinutes = c.TravelToClient.EtaMinutes ?? 0,
            DistanceMiles = c.TravelToClient.DistanceMiles ?? 0,
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