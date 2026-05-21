using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFH.Booking.Infrastructure.Location;

public sealed class LocationRouteTimeClient : ILocationRouteTimeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<LocationRouteTimeClient> _logger;

    public LocationRouteTimeClient(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<LocationRouteTimeClient> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<LocationRouteTimeResult> CalculateAsync(
        LocationRouteTimeRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        var contractRequest = new RouteTimeRequestDto
        {
            CorrelationId = request.CorrelationId,
            DepartAt = request.DepartAt,
            Source = ToDto(request.Source),
            Destination = ToDto(request.Destination)
        };

        _logger.LogInformation(
            "Sending location route-time request. CorrelationId={CorrelationId} DepartAt={DepartAt}",
            contractRequest.CorrelationId,
            contractRequest.DepartAt);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/location/route-time")
        {
            Content = JsonContent.Create(contractRequest, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            httpRequest.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(httpRequest, _options.InternalToken);

        using var response = await _http.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var data = await ReadEnvelopedOrRawAsync<RouteTimeResponseDto>(
                response,
                request.CorrelationId,
                ct);
            return data is null ? Failed(request.CorrelationId) : ToDomainResult(data);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "Location route-time call failed. Status={Status} Body={Body}",
            (int)response.StatusCode,
            body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Location service rejected the route-time request (check internal bearer configuration).");

        return Failed(request.CorrelationId);
    }

    private static RouteTimeCoordinatesDto ToDto(LocationTravelCoordinates coordinates)
        => new()
        {
            Latitude = coordinates.Latitude,
            Longitude = coordinates.Longitude
        };

    private static LocationRouteTimeResult ToDomainResult(RouteTimeResponseDto response)
    {
        return new LocationRouteTimeResult
        {
            CorrelationId = response.CorrelationId,
            TravelTimeMinutes = response.TravelTimeMinutes,
            TravelDistanceMiles = response.TravelDistanceMiles,
            Status = response.Status switch
            {
                RouteTimeStatusDto.Succeeded => LocationRouteTimeStatus.Succeeded,
                RouteTimeStatusDto.RouteUnavailable => LocationRouteTimeStatus.RouteUnavailable,
                _ => LocationRouteTimeStatus.Failed
            },
            Warnings = response.Warnings?.Select(warning => warning.Message).ToList() ?? []
        };
    }

    private static LocationRouteTimeResult Failed(string? correlationId)
        => new()
        {
            CorrelationId = correlationId,
            Status = LocationRouteTimeStatus.Failed
        };

    private async Task<T?> ReadEnvelopedOrRawAsync<T>(
        HttpResponseMessage response,
        string? correlationId,
        CancellationToken ct)
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
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Location route-time returned malformed JSON. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                (int)response.StatusCode);

            return default;
        }
    }

    private sealed class ApiEnvelope<T>
    {
        public T? Data { get; set; }
    }

    private sealed class RouteTimeRequestDto
    {
        public string? CorrelationId { get; set; }
        public DateTimeOffset DepartAt { get; set; }
        public RouteTimeCoordinatesDto Source { get; set; } = new();
        public RouteTimeCoordinatesDto Destination { get; set; } = new();
    }

    private sealed class RouteTimeResponseDto
    {
        public string? CorrelationId { get; set; }
        public int? TravelTimeMinutes { get; set; }
        public double? TravelDistanceMiles { get; set; }
        public RouteTimeStatusDto Status { get; set; }
        public List<ApiWarningDto>? Warnings { get; set; }
    }

    private sealed class RouteTimeCoordinatesDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private enum RouteTimeStatusDto
    {
        Succeeded = 0,
        RouteUnavailable = 1,
        Failed = 2
    }

    private sealed class ApiWarningDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
