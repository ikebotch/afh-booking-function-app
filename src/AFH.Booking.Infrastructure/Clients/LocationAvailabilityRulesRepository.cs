using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class LocationAvailabilityRulesRepository : IAvailabilityRulesRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocationServiceOptions _options;
    private readonly IInternalServiceAuthenticator _authenticator;
    private readonly ILogger<LocationAvailabilityRulesRepository> _logger;

    public LocationAvailabilityRulesRepository(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<LocationAvailabilityRulesRepository> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<AvailabilityRulesOptions?> GetActiveRulesAsync(CancellationToken ct, string projectContext = "Booking")
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogWarning("Location adviser availability rules lookup skipped because LocationService:BaseUrl is missing.");
            return null;
        }

        var context = string.IsNullOrWhiteSpace(projectContext) ? "Booking" : projectContext.Trim();
        var path = $"/api/v1/admin/advisers/availability-rules/active?projectContext={Uri.EscapeDataString(context)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(request, _options.InternalToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Location adviser availability rules request failed. Status={Status} Body={Body}",
                (int)response.StatusCode,
                body);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Location service rejected the adviser availability rules request (check internal bearer configuration).");

            return null;
        }

        var dto = await ReadEnvelopedOrRawAsync(response, ct);
        return dto is null ? null : ToOptions(dto);
    }

    private async Task<AvailabilityRulesResponseDto?> ReadEnvelopedOrRawAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var envelope = JsonSerializer.Deserialize<ApiEnvelope<AvailabilityRulesResponseDto>>(json, JsonOptions);
            if (envelope?.Data is not null)
                return envelope.Data;

            return JsonSerializer.Deserialize<AvailabilityRulesResponseDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Location adviser availability rules returned malformed JSON.");
            return null;
        }
    }

    private static AvailabilityRulesOptions ToOptions(AvailabilityRulesResponseDto dto)
        => new()
        {
            MinimumAppointmentMinutes = dto.MinimumAppointmentMinutes,
            DefaultWorkingDayStart = dto.DefaultWorkingDayStart,
            DefaultWorkingDayEnd = dto.DefaultWorkingDayEnd,
            CapacityWindowDays = dto.CapacityWindowDays,
            WorkingPatterns = dto.WorkingPatterns
                .Select(x => new AdviserWorkingPatternOptions
                {
                    AdviserId = x.AdviserId,
                    Start = x.Start,
                    End = x.End
                })
                .ToList(),
            CapacityLimits = dto.CapacityLimits
                .Select(x => new AdviserCapacityOptions
                {
                    AdviserId = x.AdviserId,
                    MaxActiveBookings = x.MaxActiveBookings,
                    DailyLimit = x.DailyLimit,
                    WeeklyLimit = x.WeeklyLimit,
                    MonthlyLimit = x.MonthlyLimit
                })
                .ToList()
        };

    private sealed class ApiEnvelope<T>
    {
        public T? Data { get; set; }
    }

    private sealed class AvailabilityRulesResponseDto
    {
        public int MinimumAppointmentMinutes { get; set; } = 1;
        public string DefaultWorkingDayStart { get; set; } = "08:00";
        public string DefaultWorkingDayEnd { get; set; } = "17:00";
        public int CapacityWindowDays { get; set; } = 1;
        public List<WorkingPatternDto> WorkingPatterns { get; set; } = [];
        public List<CapacityLimitDto> CapacityLimits { get; set; } = [];
    }

    private sealed class WorkingPatternDto
    {
        public string AdviserId { get; set; } = string.Empty;
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
    }

    private sealed class CapacityLimitDto
    {
        public string AdviserId { get; set; } = string.Empty;
        public int MaxActiveBookings { get; set; }
        public int? DailyLimit { get; set; }
        public int? WeeklyLimit { get; set; }
        public int? MonthlyLimit { get; set; }
    }
}
