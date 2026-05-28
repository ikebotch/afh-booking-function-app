using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AFH.Notification.Infrastructure.Delivery.Sms;

public sealed class TwilioSmsSender : ISmsProviderSender
{
    private readonly HttpClient _httpClient;
    private readonly TwilioSmsOptions _options;

    public TwilioSmsSender(HttpClient httpClient, IOptions<TwilioSmsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _options.Validate();
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDeliveryRequest request, CancellationToken ct)
    {
        var values = new Dictionary<string, string>
        {
            ["To"] = request.Recipient.MobileNumber!,
            ["Body"] = request.TextBody
        };

        if (!string.IsNullOrWhiteSpace(_options.MessagingServiceSid))
            values["MessagingServiceSid"] = _options.MessagingServiceSid!;
        else
            values["From"] = _options.FromPhoneNumber!;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"2010-04-01/Accounts/{_options.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(values)
        };
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Twilio SMS send failed with HTTP {(int)response.StatusCode}.");

        var providerMessageId = TryReadJsonString(json, "sid") ?? $"twilio-{Guid.NewGuid():N}";
        return new NotificationDeliveryResult("Sent", providerMessageId, "Twilio");
    }

    private static string? TryReadJsonString(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
