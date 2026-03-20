using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Infrastructure.Persistence.Models;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class CalendarNotificationRepository : ICalendarNotificationRepository
{
    private readonly BookingDbContext _db;

    public CalendarNotificationRepository(BookingDbContext db)
        => _db = db;

    public async Task<CalendarNotificationReceipt?> AddAsync(CalendarNotificationReceipt receipt, CancellationToken ct)
    {
        var model = new CalendarNotificationReceiptModel
        {
            Id = receipt.Id,
            SubscriptionId = receipt.SubscriptionId,
            EventId = receipt.EventId,
            ChangeType = receipt.ChangeType,
            ReceivedUtc = receipt.ReceivedUtc,
            RawPayload = receipt.RawPayload,
             ClientState = receipt.ClientState,
            Accepted = receipt.Accepted,
            RejectReason = receipt.RejectReason
        };
        await _db.Set<CalendarNotificationReceiptModel>().AddAsync(model, ct);

        return receipt;
    }
}