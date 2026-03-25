namespace AFH.Booking.Application.Abstractions.Governance;

public interface IOperationalNotificationService
{
    Task NotifyAdviserAsync(
        string adviserId,
        string bookingId,
        string? transactionId,
        string? transactionRef,
        string eventType,
        string message,
        string? correlationId,
        CancellationToken ct);

    Task NotifyManagersAsync(
        IReadOnlyList<string> recipients,
        string bookingId,
        string? transactionId,
        string? transactionRef,
        string eventType,
        string message,
        string? correlationId,
        CancellationToken ct);
}
