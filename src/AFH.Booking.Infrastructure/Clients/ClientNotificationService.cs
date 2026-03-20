using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class ClientNotificationService : IClientNotificationService
{
    private readonly BookingDbContext _db;
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IClientDirectory _clients;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationsOptions _options;
    private readonly ILogger<ClientNotificationService> _logger;

    public ClientNotificationService(
        BookingDbContext db,
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IClientDirectory clients,
        IHttpClientFactory httpClientFactory,
        IOptions<NotificationsOptions> options,
        ILogger<ClientNotificationService> logger)
    {
        _db = db;
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _clients = clients;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NotificationDispatchResponse> SendBookingNotificationAsync(
        string bookingId,
        string eventType,
        string? message,
        bool sendSms,
        bool sendEmail,
        CancellationToken ct)
    {
        var hold = await _holds.GetAsync(bookingId, ct);
        if (hold is null)
            throw new InvalidOperationException($"Hold '{bookingId}' was not found.");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            throw new InvalidOperationException($"Slot '{hold.SlotId}' was not found.");

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            throw new InvalidOperationException($"Transaction '{slot.TransactionId}' was not found.");

        var client = await _clients.GetAsync(tx.TransactionRef, ct);
        var defaultSmsBody = string.IsNullOrWhiteSpace(message)
            ? $"Your booking has been updated ({eventType}) for {slot.StartUtc:yyyy-MM-dd HH:mm} with {slot.AdviserName}."
            : message.Trim();

        var clientDisplayName = $"{client?.FirstName} {client?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(clientDisplayName))
            clientDisplayName = null;

        var emailTemplate = BookingNotificationEmailTemplate.Build(
            eventType: eventType,
            clientDisplayName: clientDisplayName,
            adviserName: slot.AdviserName,
            startUtc: slot.StartUtc,
            endUtc: slot.EndUtc,
            timezoneId: tx.Timezone,
            isRemote: tx.IsRemote,
            customMessage: message);

        var smsStatus = sendSms ? "Pending" : "Skipped";
        var emailStatus = sendEmail ? "Pending" : "Skipped";
        var providerMessageId = Guid.NewGuid().ToString("N")[..20];

        if (sendSms)
        {
            smsStatus = await SendSmsAsync(client?.Phone, defaultSmsBody, ct);
        }

        if (sendEmail)
        {
            emailStatus = string.IsNullOrWhiteSpace(client?.Email)
                ? "Skipped"
                : (_options.EmailEnabled ? "Composed" : "ConfiguredOff");
        }

        // Persist plain text because current provider path renders stored content as text/plain.
        var persistedBody = sendEmail ? emailTemplate.TextBody : defaultSmsBody;
        if (persistedBody.Length > 3900)
            persistedBody = persistedBody[..3900];

        var dispatch = new NotificationDispatchModel
        {
            Id = Guid.NewGuid().ToString("N"),
            BookingId = bookingId,
            EventType = eventType,
            SmsRequested = sendSms,
            EmailRequested = sendEmail,
            SmsStatus = smsStatus,
            EmailStatus = emailStatus,
            RecipientPhone = client?.Phone,
            RecipientEmail = client?.Email,
            ProviderMessageId = providerMessageId,
            MessageBody = persistedBody,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.NotificationDispatches.Add(dispatch);
        await _db.SaveChangesAsync(ct);

        return new NotificationDispatchResponse
        {
            DispatchId = dispatch.Id,
            BookingId = dispatch.BookingId,
            EventType = dispatch.EventType,
            SmsRequested = dispatch.SmsRequested,
            EmailRequested = dispatch.EmailRequested,
            SmsStatus = dispatch.SmsStatus,
            EmailStatus = dispatch.EmailStatus,
            ProviderMessageId = dispatch.ProviderMessageId,
            CreatedUtc = dispatch.CreatedUtc
        };
    }

    private async Task<string> SendSmsAsync(string? phone, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Skipped";

        if (!_options.SmsEnabled || string.IsNullOrWhiteSpace(_options.SmsBaseUrl))
            return "ConfiguredOff";

        try
        {
            var http = _httpClientFactory.CreateClient("sms-provider");
            var response = await http.PostAsJsonAsync(
                "/messages",
                new
                {
                    to = phone,
                    from = _options.SmsSenderId,
                    message
                },
                ct);

            return response.IsSuccessStatusCode ? "Sent" : $"Failed:{(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS dispatch failed for phone {Phone}", phone);
            return "Failed";
        }
    }
}
