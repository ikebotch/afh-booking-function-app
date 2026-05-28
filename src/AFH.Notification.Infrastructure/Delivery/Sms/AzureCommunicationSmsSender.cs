using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Delivery.Sms;

public sealed class AzureCommunicationSmsSender : ISmsProviderSender
{
    private const string ApiVersion = "2021-03-07";
    private readonly HttpClient _httpClient;
    private readonly AzureCommunicationSmsOptions _options;
    private readonly Uri _endpoint;
    private readonly byte[]? _accessKey;

    public AzureCommunicationSmsSender(HttpClient httpClient, IOptions<AzureCommunicationSmsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();

        (_endpoint, _accessKey) = ResolveEndpointAndKey(_options);
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
    {
        if (_options.UseManagedIdentity)
            throw new NotSupportedException("ACS SMS managed identity sending is not implemented in this host yet. Configure ConnectionString for Sprint 7 SMS delivery.");

        var smsBody = request.TextBody;
        var payload = new
        {
            from = _options.FromPhoneNumber,
            smsRecipients = new[] { new { to = request.Recipient.MobileNumber } },
            message = smsBody,
            smsSendOptions = new { enableDeliveryReport = _options.DeliveryReportEnabled }
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var pathAndQuery = $"/sms?api-version={ApiVersion}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, pathAndQuery))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        Sign(httpRequest, pathAndQuery, json);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ACS SMS send failed with HTTP {(int)response.StatusCode}.");

        var providerMessageId = TryReadAcsMessageId(responseJson) ?? $"acs-sms-{Guid.NewGuid():N}";
        return new NotificationDeliveryResult("Sent", providerMessageId, "AzureCommunicationServices");
    }

    private void Sign(HttpRequestMessage request, string pathAndQuery, string body)
    {
        var date = DateTimeOffset.UtcNow.ToString("r");
        var contentHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var stringToSign = $"{request.Method.Method}\n{pathAndQuery}\n{date};{_endpoint.Host};{contentHash}";
        using var hmac = new HMACSHA256(_accessKey!);
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        request.Headers.TryAddWithoutValidation("x-ms-date", date);
        request.Headers.TryAddWithoutValidation("x-ms-content-sha256", contentHash);
        request.Headers.TryAddWithoutValidation("Authorization", $"HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}");
    }

    private static (Uri Endpoint, byte[]? AccessKey) ResolveEndpointAndKey(AzureCommunicationSmsOptions options)
    {
        if (options.UseManagedIdentity)
            return (new Uri(options.Endpoint!, UriKind.Absolute), null);

        var parts = options.ConnectionString!
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"{AzureCommunicationSmsOptions.SectionName}:ConnectionString must contain endpoint.");
        if (!parts.TryGetValue("accesskey", out var accessKey) || string.IsNullOrWhiteSpace(accessKey))
            throw new InvalidOperationException($"{AzureCommunicationSmsOptions.SectionName}:ConnectionString must contain accesskey.");

        return (new Uri(endpoint, UriKind.Absolute), Convert.FromBase64String(accessKey));
    }

    private static string? TryReadAcsMessageId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("receipts", out var receipts) &&
            receipts.ValueKind == JsonValueKind.Array &&
            receipts.GetArrayLength() > 0 &&
            receipts[0].TryGetProperty("messageId", out var messageId) &&
            messageId.ValueKind == JsonValueKind.String)
        {
            return messageId.GetString();
        }

        return null;
    }
}
