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
using System.Text.Json.Serialization;

namespace AFH.Booking.Infrastructure.Location;

public sealed class LocationTravelCoverageClient : ILocationTravelCoverageClient
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

        bool useTimeDependent = request.TimingMode == LocationTravelTimingMode.DepartureTime || _options.UseTimeDependentEvaluation;
        var contractRequest = ToContractRequest(request, useTimeDependent);

        _logger.LogInformation(
            "Sending travel coverage request. TravelEvaluationMode={TravelEvaluationMode} SlotResponseMode={SlotResponseMode} CorrelationId={CorrelationId}",
            contractRequest.TimeContext.TravelEvaluationMode,
            contractRequest.TimeContext.SlotResponseMode,
            contractRequest.RequestContext.CorrelationId);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/api/v1/location/travel-coverage")
        {
            Content = JsonContent.Create(contractRequest, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            httpRequest.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(httpRequest, _options.InternalToken);

        using var response = await _http.SendAsync(httpRequest, ct);
        if (response.IsSuccessStatusCode)
        {
            var data = await ReadEnvelopedOrRawAsync<TravelCoverageResponseDto>(
                response,
                request.CorrelationId,
                ct);

            return data is null ? MalformedCoverageResult(request) : ToDomainResult(data);
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

    private static TravelCoverageRequestDto ToContractRequest(LocationTravelCoverageRequest request, bool useTimeDependent)
    {
        return new TravelCoverageRequestDto
        {
            SourcePostcode = request.SourcePostcode,
            TimeContext = new TravelCoverageTimeContextDto
            {
                TravelEvaluationMode = useTimeDependent ? TravelEvaluationModeDto.TimeDependent : TravelEvaluationModeDto.TimeIndependent,
                SlotResponseMode = SlotResponseModeDto.Summary,
                StartTime = request.RequestedDepartureTime,
                EndTime = request.RequestedEndTime,
                SearchIntervalMinutes = request.SearchIntervalMinutes
            },
            Destinations = request.Destinations.Select(destination => new TravelCoverageDestinationRequestDto
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                MaxTravelTimeMinutes = destination.MaxTravelTimeMinutes,
                MaxDistanceMiles = destination.MaxDistanceMiles
            }).ToList(),
            RequestContext = new LocationRequestContextDto
            {
                CorrelationId = request.CorrelationId
            }
        };
    }

    private static LocationTravelCoverageResult ToDomainResult(TravelCoverageResponseDto response)
    {
        return new LocationTravelCoverageResult
        {
            SourcePostcode = response.SourcePostcode,
            SourceCoordinates = ToDomainCoordinates(response.SourceCoordinates),
            Destinations = response.Destinations.Select(destination =>
            {
                var firstSlot = destination.Slots?.FirstOrDefault();

                return new LocationTravelCoverageOutcome
                {
                    CorrelationId = destination.CorrelationId,
                    Postcode = destination.Postcode,
                    Status = ToDomainStatus(destination.Status),
                    Coordinates = ToDomainCoordinates(destination.Coordinates),
                    Route = firstSlot is null
                        ? null
                        : new LocationTravelRouteOutcome
                        {
                            TravelTimeMinutes = firstSlot.TravelTimeMinutes,
                            TravelDistanceMiles = firstSlot.TravelDistanceMiles,
                            Confidence = "High",
                            ResolutionSource = LocationTravelResolutionSource.Unknown
                        },
                    Coverage = firstSlot is null
                        ? null
                        : new LocationCoverageOutcome
                        {
                            IsWithinCoverage = firstSlot.IsWithinCoverage,
                            MaxTravelTimeMinutes = null,
                            MaxDistanceMiles = null
                        }
                };
            }).ToList()
        };
    }

    private static LocationTravelCoverageResult MalformedCoverageResult(LocationTravelCoverageRequest request)
    {
        return new LocationTravelCoverageResult
        {
            SourcePostcode = request.SourcePostcode,
            Destinations = request.Destinations.Select(destination => new LocationTravelCoverageOutcome
            {
                CorrelationId = destination.CorrelationId,
                Postcode = destination.Postcode,
                Status = LocationTravelCoverageStatus.Failed
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
                "Location travel coverage returned malformed JSON. CorrelationId={CorrelationId} Status={Status}",
                correlationId,
                (int)response.StatusCode);

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
        public LocationRequestContextDto RequestContext { get; set; } = new();
    }

    private sealed class TravelCoverageTimeContextDto
    {
        public TravelEvaluationModeDto TravelEvaluationMode { get; set; } = TravelEvaluationModeDto.TimeIndependent;
        public SlotResponseModeDto SlotResponseMode { get; set; } = SlotResponseModeDto.Summary;
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public int? SearchIntervalMinutes { get; set; }
    }

    private enum TravelEvaluationModeDto
    {
        TimeIndependent = 0,
        TimeDependent = 1
    }

    private enum SlotResponseModeDto
    {
        Grouped = 0,
        Expanded = 1,
        Summary = 2
    }

    private sealed class TravelCoverageDestinationRequestDto
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public int? MaxTravelTimeMinutes { get; set; }
        public double? MaxDistanceMiles { get; set; }
    }

    private sealed class LocationRequestContextDto
    {
        public string? CorrelationId { get; set; }
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
        public List<TravelCoverageSlotDto>? Slots { get; set; }
        public List<ApiWarningDto>? Warnings { get; set; }
    }

    private sealed class TravelCoverageSlotDto
    {
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public int TravelTimeMinutes { get; set; }
        public double TravelDistanceMiles { get; set; }
        public bool IsWithinCoverage { get; set; }
    }

    private sealed class ApiWarningDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private enum TravelCoverageStatusDto
    {
        Succeeded = 0,
        SourcePostcodeUnresolved = 1,
        DestinationPostcodeUnresolved = 2,
        RouteUnavailable = 3,
        Failed = 4
    }

    private sealed class LocationCoordinatesDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
