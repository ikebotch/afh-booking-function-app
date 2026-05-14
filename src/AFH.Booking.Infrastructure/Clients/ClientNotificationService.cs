using System.Net.Http.Json;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class ClientNotificationService : IClientNotificationService, INotificationService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IClientDirectory _clients;
    private readonly IEmailNotificationSender _emailSender;
    private readonly INotificationDispatchRepository _dispatches;
    private readonly IUnitOfWork _uow;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationsOptions _options;
    private readonly ILogger<ClientNotificationService> _logger;

    public ClientNotificationService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IClientDirectory clients,
        IEmailNotificationSender emailSender,
        INotificationDispatchRepository dispatches,
        IUnitOfWork uow,
        IHttpClientFactory httpClientFactory,
        IOptions<NotificationsOptions> options,
        ILogger<ClientNotificationService> logger)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _clients = clients;
        _emailSender = emailSender;
        _dispatches = dispatches;
        _uow = uow;
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
        return await SendBookingNotificationAsync(
            new NotificationDispatchRequest(
                bookingId,
                eventType,
                message,
                sendSms,
                sendEmail),
            ct);
    }

    public async Task<NotificationDispatchResponse> SendBookingNotificationAsync(
        NotificationDispatchRequest request,
        CancellationToken ct)
    {
        var hold = await _holds.GetAsync(request.BookingId, ct);
        if (hold is null)
            throw new InvalidOperationException($"Hold '{request.BookingId}' was not found.");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            throw new InvalidOperationException($"Slot '{hold.SlotId}' was not found.");

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            throw new InvalidOperationException($"Transaction '{slot.TransactionId}' was not found.");

        var client = await _clients.GetAsync(tx.TransactionRef, ct);
        var defaultSmsBody = string.IsNullOrWhiteSpace(request.Message)
            ? $"Your booking has been updated ({request.EventType}) for {slot.StartUtc:yyyy-MM-dd HH:mm} with {slot.AdviserName}."
            : request.Message!.Trim();

        var clientDisplayName = $"{client?.FirstName} {client?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(clientDisplayName))
            clientDisplayName = null;

        var emailTemplate = BookingNotificationEmailTemplate.Build(
            eventType: request.EventType,
            clientDisplayName: clientDisplayName,
            adviserName: slot.AdviserName,
            startUtc: slot.StartUtc,
            endUtc: slot.EndUtc,
            timezoneId: tx.Timezone,
            isRemote: tx.IsRemote,
            customMessage: request.Message);

        var smsStatus = request.SendSms ? "Pending" : "Skipped";
        var emailStatus = request.SendEmail ? "Pending" : "Skipped";
        var providerMessageId = Guid.NewGuid().ToString("N")[..20];

        if (request.SendSms)
        {
            smsStatus = await SendSmsAsync(client?.Phone, defaultSmsBody, ct);
        }

        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(client?.Email))
            {
                emailStatus = "Skipped";
            }
            else if (!_options.EmailEnabled)
            {
                emailStatus = "ConfiguredOff";
            }
            else
            {
                var emailResult = await _emailSender.SendAsync(
                    new EmailNotificationMessage(
                        client.Email,
                        emailTemplate.Subject,
                        emailTemplate.HtmlBody,
                        emailTemplate.TextBody),
                    ct);

                emailStatus = emailResult.Status;
                providerMessageId = emailResult.ProviderMessageId ?? providerMessageId;
            }
        }

        // Persist plain text because current provider path renders stored content as text/plain.
        var persistedBody = request.SendEmail ? emailTemplate.TextBody : defaultSmsBody;
        if (persistedBody.Length > 3900)
            persistedBody = persistedBody[..3900];

        var outcomeCode = ResolveOutcomeCode(smsStatus, emailStatus);
        var failureDetails = outcomeCode == LifecycleStepStatuses.Failed
            ? BuildFailureDetails(smsStatus, emailStatus)
            : null;

        var dispatchId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        await _dispatches.AddAsync(new NotificationDispatchRecord(
            Id: dispatchId,
            BookingId: request.BookingId,
            TransactionId: tx.Id,
            TransactionRef: tx.TransactionRef,
            EventType: request.EventType,
            SmsRequested: request.SendSms,
            EmailRequested: request.SendEmail,
            SmsStatus: smsStatus,
            EmailStatus: emailStatus,
            OutcomeCode: outcomeCode,
            FailureDetails: failureDetails,
            RecipientPhone: client?.Phone,
            RecipientEmail: client?.Email,
            ProviderMessageId: providerMessageId,
            MessageBody: persistedBody,
            LifecycleEventId: request.LifecycleEventId,
            CorrelationId: request.CorrelationId,
            CreatedUtc: now,
            UpdatedUtc: now), ct);
        await _uow.SaveChangesAsync(ct);

        return new NotificationDispatchResponse
        {
            DispatchId = dispatchId,
            BookingId = request.BookingId,
            EventType = request.EventType,
            SmsRequested = request.SendSms,
            EmailRequested = request.SendEmail,
            SmsStatus = smsStatus,
            EmailStatus = emailStatus,
            ProviderMessageId = providerMessageId,
            CreatedUtc = now
        };
    }

    private static string ResolveOutcomeCode(string smsStatus, string emailStatus)
    {
        if (smsStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            emailStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return LifecycleStepStatuses.Failed;
        }

        if (smsStatus == "Skipped" && emailStatus == "Skipped")
            return LifecycleStepStatuses.Skipped;

        return LifecycleStepStatuses.Succeeded;
    }

    private static string? BuildFailureDetails(string smsStatus, string emailStatus)
    {
        var parts = new List<string>();
        if (smsStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            parts.Add($"SMS={smsStatus}");
        if (emailStatus.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            parts.Add($"Email={emailStatus}");

        return parts.Count == 0 ? null : string.Join("; ", parts);
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
