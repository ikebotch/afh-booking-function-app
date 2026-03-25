using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Clients;

public sealed class OperationalNotificationService : IOperationalNotificationService
{
    private readonly INotificationDispatchRepository _dispatches;
    private readonly IUnitOfWork _uow;
    private readonly NotificationsOptions _options;

    public OperationalNotificationService(
        INotificationDispatchRepository dispatches,
        IUnitOfWork uow,
        IOptions<NotificationsOptions> options)
    {
        _dispatches = dispatches;
        _uow = uow;
        _options = options.Value;
    }

    public Task NotifyAdviserAsync(
        string adviserId,
        string bookingId,
        string? transactionId,
        string? transactionRef,
        string eventType,
        string message,
        string? correlationId,
        CancellationToken ct)
        => AddAsync(adviserId, bookingId, transactionId, transactionRef, eventType, message, correlationId, ct);

    public async Task NotifyManagersAsync(
        IReadOnlyList<string> recipients,
        string bookingId,
        string? transactionId,
        string? transactionRef,
        string eventType,
        string message,
        string? correlationId,
        CancellationToken ct)
    {
        foreach (var recipient in recipients.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await AddAsync(recipient.Trim(), bookingId, transactionId, transactionRef, eventType, message, correlationId, ct);
        }
    }

    private async Task AddAsync(
        string recipientEmail,
        string bookingId,
        string? transactionId,
        string? transactionRef,
        string eventType,
        string message,
        string? correlationId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await _dispatches.AddAsync(new NotificationDispatchRecord(
            Id: Guid.NewGuid().ToString("N"),
            BookingId: bookingId,
            TransactionId: transactionId,
            TransactionRef: transactionRef,
            EventType: eventType,
            SmsRequested: false,
            EmailRequested: true,
            SmsStatus: "Skipped",
            EmailStatus: _options.EmailEnabled ? "Composed" : "ConfiguredOff",
            OutcomeCode: _options.EmailEnabled ? LifecycleStepStatuses.Succeeded : LifecycleStepStatuses.Skipped,
            FailureDetails: null,
            RecipientPhone: null,
            RecipientEmail: recipientEmail,
            ProviderMessageId: Guid.NewGuid().ToString("N")[..20],
            MessageBody: message.Length > 3900 ? message[..3900] : message,
            LifecycleEventId: null,
            CorrelationId: correlationId,
            CreatedUtc: now,
            UpdatedUtc: now), ct);

        await _uow.SaveChangesAsync(ct);
    }
}
