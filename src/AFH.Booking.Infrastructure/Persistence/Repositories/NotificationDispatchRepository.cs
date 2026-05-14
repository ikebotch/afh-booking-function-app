using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class NotificationDispatchRepository : INotificationDispatchRepository
{
    private readonly BookingDbContext _db;

    public NotificationDispatchRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(NotificationDispatchRecord record, CancellationToken ct)
    {
        await _db.NotificationDispatches.AddAsync(new NotificationDispatchModel
        {
            Id = record.Id,
            BookingId = record.BookingId,
            TransactionId = record.TransactionId,
            TransactionRef = record.TransactionRef,
            EventType = record.EventType,
            SmsRequested = record.SmsRequested,
            EmailRequested = record.EmailRequested,
            SmsStatus = record.SmsStatus,
            EmailStatus = record.EmailStatus,
            OutcomeCode = record.OutcomeCode,
            FailureDetails = record.FailureDetails,
            RecipientPhone = record.RecipientPhone,
            RecipientEmail = record.RecipientEmail,
            ProviderMessageId = record.ProviderMessageId,
            MessageBody = record.MessageBody,
            LifecycleEventId = record.LifecycleEventId,
            CorrelationId = record.CorrelationId,
            CreatedUtc = record.CreatedUtc,
            UpdatedUtc = record.UpdatedUtc
        }, ct);
    }
}
