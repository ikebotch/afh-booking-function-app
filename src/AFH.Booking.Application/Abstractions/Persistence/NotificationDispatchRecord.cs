namespace AFH.Booking.Application.Abstractions.Persistence;

public sealed record NotificationDispatchRecord(
    string Id,
    string BookingId,
    string? TransactionId,
    string? TransactionRef,
    string EventType,
    bool SmsRequested,
    bool EmailRequested,
    string SmsStatus,
    string EmailStatus,
    string OutcomeCode,
    string? FailureDetails,
    string? RecipientPhone,
    string? RecipientEmail,
    string? ProviderMessageId,
    string? MessageBody,
    string? LifecycleEventId,
    string? CorrelationId,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
