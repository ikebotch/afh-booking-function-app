using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Models.BusinessContacts;
using AFH.Booking.Application.Models.Notifications;
using AFH.Booking.Domain.Options;
using AFH.Booking.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class BookingBusinessContactsClient : IBookingBusinessContactsClient
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
    private readonly ILogger<BookingBusinessContactsClient> _logger;

    public BookingBusinessContactsClient(
        HttpClient http,
        IOptions<LocationServiceOptions> options,
        IInternalServiceAuthenticator authenticator,
        ILogger<BookingBusinessContactsClient> logger)
    {
        _http = http;
        _options = options.Value;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookingBusinessContact>> GetContactsAsync(
        BookingBusinessContactSearch search,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new InvalidOperationException($"{LocationServiceOptions.SectionName}:BaseUrl is required.");

        if (search.ContactTypes.Count == 0)
            return [];

        var query = new Dictionary<string, string?>
        {
            ["context"] = "Booking",
            ["roles"] = string.Join(',', search.ContactTypes.Select(x => x.Trim()).Where(x => x.Length > 0))
        };

        AddIfPresent(query, "adviserId", search.AdviserId);
        AddIfPresent(query, "region", search.Region);
        AddIfPresent(query, "organisationId", search.OrganisationId);
        AddIfPresent(query, "clientId", search.ClientId);

        var path = AddQueryString("/api/v1/admin/business-contacts", query);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (!string.IsNullOrWhiteSpace(_options.FunctionKey))
            request.Headers.TryAddWithoutValidation("x-functions-key", _options.FunctionKey.Trim());

        _authenticator.Apply(request, _options.InternalToken);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Location business contacts request failed. Status={Status} FailureCategory={FailureCategory} Body={Body}",
                (int)response.StatusCode,
                DownstreamFailureClassifier.Classify(response.StatusCode),
                body);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new InvalidOperationException("Location service rejected the business contacts request (check internal bearer configuration).");

            return [];
        }

        var envelope = await response.Content.ReadFromJsonAsync<BusinessContactsResponseDto>(JsonOptions, ct);
        return envelope?.Contacts
            .Select(ToContact)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToArray() ?? [];
    }

    private static BookingBusinessContact? ToContact(BusinessContactDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ContactType))
            return null;

        var channels = dto.Channels
            .Select(ToChannel)
            .Where(x => x != BookingNotificationChannel.Unknown)
            .Distinct()
            .ToArray();

        return new BookingBusinessContact(
            dto.ContactType.Trim(),
            string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.ContactType.Trim() : dto.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
            string.IsNullOrWhiteSpace(dto.MobileNumber) ? null : dto.MobileNumber.Trim(),
            channels);
    }

    private static BookingNotificationChannel ToChannel(string value)
        => Enum.TryParse<BookingNotificationChannel>(value, ignoreCase: true, out var channel)
            ? channel
            : BookingNotificationChannel.Unknown;

    private static void AddIfPresent(IDictionary<string, string?> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query[key] = value.Trim();
    }

    private static string AddQueryString(string path, IReadOnlyDictionary<string, string?> query)
    {
        var values = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}")
            .ToArray();

        return values.Length == 0 ? path : $"{path}?{string.Join('&', values)}";
    }

    private sealed class BusinessContactsResponseDto
    {
        public List<BusinessContactDto> Contacts { get; set; } = [];
    }

    private sealed class BusinessContactDto
    {
        public string ContactType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public List<string> Channels { get; set; } = [];
    }
}
