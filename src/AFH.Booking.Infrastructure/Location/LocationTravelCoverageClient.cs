using AFH.Booking.Application.Abstractions.Location;
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

public sealed class LocationTravelCoverageClient : ILocationTravelCoverageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<LocationTravelCoverageClient> _logger;

    public LocationTravelCoverageClient(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<LocationTravelCoverageClient> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<LocationTravelCoverageResult> EvaluateAsync(
        LocationTravelCoverageRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/location/travel-coverage")
        {
            Content = JsonContent.Create(ToContractRequest(request), options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            httpRequest.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(httpRequest, _options.InternalToken);

        using var response = await _http.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var data = await ReadEnvelopedOrRawAsync<TravelCoverageResponseDto>(response, ct);
            return data is null ? new LocationTravelCoverageResult() : ToDomainResult(data);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning(
            "Location travel coverage call failed. Status={Status} Body={Body}",
            (int)response.StatusCode,
            body);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Location service rejected the request (check internal bearer configuration).");

        return new LocationTravelCoverageResult();
    }

    private static TravelCoverageRequestDto ToContractRequest(LocationTravelCoverageRequest request)
    {
        return new TravelCoverageRequestDto
        {
            SourcePostcode = request.SourcePostcode,
            TimeContext = new TravelCoverageTimeContextDto
            {
                RequestedDepartureTime = request.RequestedDepartureTime,
                TimingMode = request.TimingMode == LocationTravelTimingMode.DepartureTime
                    ? TravelCoverageTimingModeDto.DepartureTime
                    : TravelCoverageTimingModeDto.TimeIndependent
            },
            Destinations = request.Destinations.Select(destination => new TravelCoverageDestinationRequestDto
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                MaxDistanceMiles = destination.MaxDistanceMiles
            }).ToList(),
            Metadata = new TravelCoverageRequestMetadataDto
            {
                AppointmentType = request.AppointmentType,
                Channel = request.Channel
            },
            RequestContext = new LocationRequestContextDto
            {
                CorrelationId = request.CorrelationId,
                RequestedBy = request.RequestedBy
            }
        };
    }

    private static LocationTravelCoverageResult ToDomainResult(TravelCoverageResponseDto response)
    {
        return new LocationTravelCoverageResult
        {
            SourcePostcode = response.SourcePostcode,
            SourceCoordinates = ToDomainCoordinates(response.SourceCoordinates),
            Destinations = response.Destinations.Select(destination => new LocationTravelCoverageOutcome
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                Status = ToDomainStatus(destination.Status),
                Coordinates = ToDomainCoordinates(destination.Coordinates),
                Route = destination.Route is null
                    ? null
                    : new LocationTravelRouteOutcome
                    {
                        TravelTimeMinutes = destination.Route.TravelTimeMinutes,
                        TravelDistanceMiles = destination.Route.TravelDistanceMiles,
                        Confidence = destination.Route.Confidence,
                        ResolutionSource = ToDomainResolutionSource(destination.Route.ResolutionSource)
                    },
                Coverage = destination.Coverage is null
                    ? null
                    : new LocationCoverageOutcome
                    {
                        IsWithinCoverage = destination.Coverage.IsWithinCoverage,
                        MaxTravelTimeMinutes = destination.Coverage.MaxTravelTimeMinutes,
                        MaxDistanceMiles = destination.Coverage.MaxDistanceMiles
                    }
            }).ToList()
        };
    }

    private static LocationTravelCoordinates? ToDomainCoordinates(LocationCoordinatesDto? coordinates)
    {
        return coordinates is null
            ? null
            : new LocationTravelCoordinates
            {
                Latitude = coordinates.Latitude,
                Longitude = coordinates.Longitude
            };
    }

    private static LocationTravelCoverageStatus ToDomainStatus(TravelCoverageStatusDto status)
    {
        return status switch
        {
            TravelCoverageStatusDto.Succeeded => LocationTravelCoverageStatus.Succeeded,
            TravelCoverageStatusDto.SourcePostcodeUnresolved => LocationTravelCoverageStatus.SourcePostcodeUnresolved,
            TravelCoverageStatusDto.DestinationPostcodeUnresolved => LocationTravelCoverageStatus.DestinationPostcodeUnresolved,
            TravelCoverageStatusDto.RouteUnavailable => LocationTravelCoverageStatus.RouteUnavailable,
            _ => LocationTravelCoverageStatus.Failed
        };
    }

    private static LocationTravelResolutionSource ToDomainResolutionSource(TravelRouteResolutionSourceDto source)
    {
        return source switch
        {
            TravelRouteResolutionSourceDto.Cache => LocationTravelResolutionSource.Cache,
            TravelRouteResolutionSourceDto.Database => LocationTravelResolutionSource.Database,
            TravelRouteResolutionSourceDto.AzureMaps => LocationTravelResolutionSource.AzureMaps,
            _ => LocationTravelResolutionSource.Unknown
        };
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

    private sealed class ApiEnvelope<T>
    {
        public T? Data { get; set; }
    }

    private sealed class TravelCoverageRequestDto
    {
        public string SourcePostcode { get; set; } = string.Empty;
        public TravelCoverageTimeContextDto TimeContext { get; set; } = new();
        public List<TravelCoverageDestinationRequestDto> Destinations { get; set; } = new();
        public TravelCoverageRequestMetadataDto Metadata { get; set; } = new();
        public LocationRequestContextDto RequestContext { get; set; } = new();
    }

    private sealed class TravelCoverageTimeContextDto
    {
        public DateTimeOffset? RequestedDepartureTime { get; set; }
        public TravelCoverageTimingModeDto TimingMode { get; set; }
    }

    private enum TravelCoverageTimingModeDto
    {
        TimeIndependent = 0,
        DepartureTime = 1
    }

    private sealed class TravelCoverageDestinationRequestDto
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public int? MaxTravelTimeMinutes { get; set; }
        public double? MaxDistanceMiles { get; set; }
    }

    private sealed class TravelCoverageRequestMetadataDto
    {
        public string? AppointmentType { get; set; }
        public string? Channel { get; set; }
    }

    private sealed class LocationRequestContextDto
    {
        public string? CorrelationId { get; set; }
        public string? RequestedBy { get; set; }
    }

    private sealed class TravelCoverageResponseDto
    {
        public string SourcePostcode { get; set; } = string.Empty;
        public LocationCoordinatesDto? SourceCoordinates { get; set; }
        public List<TravelCoverageDestinationOutcomeDto> Destinations { get; set; } = new();
    }

    private sealed class TravelCoverageDestinationOutcomeDto
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public TravelCoverageStatusDto Status { get; set; }
        public LocationCoordinatesDto? Coordinates { get; set; }
        public TravelRouteOutcomeDto? Route { get; set; }
        public CoverageOutcomeDto? Coverage { get; set; }
    }

    private enum TravelCoverageStatusDto
    {
        Succeeded = 0,
        SourcePostcodeUnresolved = 1,
        DestinationPostcodeUnresolved = 2,
        RouteUnavailable = 3,
        Failed = 4
    }

    private sealed class TravelRouteOutcomeDto
    {
        public int TravelTimeMinutes { get; set; }
        public double TravelDistanceMiles { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public TravelRouteResolutionSourceDto ResolutionSource { get; set; }
    }

    private enum TravelRouteResolutionSourceDto
    {
        Unknown = 0,
        Cache = 1,
        Database = 2,
        AzureMaps = 3
    }

    private sealed class CoverageOutcomeDto
    {
        public bool IsWithinCoverage { get; set; }
        public int? MaxTravelTimeMinutes { get; set; }
        public double? MaxDistanceMiles { get; set; }
    }

    private sealed class LocationCoordinatesDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
