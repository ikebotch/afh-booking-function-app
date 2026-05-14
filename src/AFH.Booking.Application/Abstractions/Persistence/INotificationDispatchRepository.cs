namespace AFH.Booking.Application.Abstractions.Persistence;

public interface INotificationDispatchRepository
{
    Task AddAsync(NotificationDispatchRecord record, CancellationToken ct);
}
