using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class CalendarEventSnapshotRepository : ICalendarEventSnapshotRepository
{
    private readonly BookingDbContext _db;

    public CalendarEventSnapshotRepository(BookingDbContext db)
        => _db = db;

    public async Task AddAsync(CalendarEventSnapshot snapshot, CancellationToken ct)
    {
        var model = new CalendarEventSnapshotModel
        {
            Id = snapshot.Id,
            ReceiptId = snapshot.ReceiptId,

            UserId = snapshot.UserId,
            ProviderEventId = snapshot.ProviderEventId,

            CalendarId = snapshot.CalendarId,
            Subject = snapshot.Subject,
            StartUtc = snapshot.StartUtc,
            EndUtc = snapshot.EndUtc,
            IsCancelled = snapshot.IsCancelled,

            ChangeKey = snapshot.ChangeKey,
            ICalUId = snapshot.ICalUId,

            FetchedUtc = snapshot.FetchedUtc,
            FetchError = snapshot.FetchError
        };

        await _db.Set<CalendarEventSnapshotModel>().AddAsync(model, ct);
    }

    public async Task<CalendarEventSnapshot?> GetLatestAsync(string userId, string providerEventId, CancellationToken ct)
    {
        var model = await _db.Set<CalendarEventSnapshotModel>()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ProviderEventId == providerEventId)
            .OrderByDescending(x => x.FetchedUtc)
            .FirstOrDefaultAsync(ct);

        if (model is null) return null;

        return CalendarEventSnapshot.Rehydrate(
            id: model.Id,
            receiptId: model.ReceiptId,
            userId: model.UserId,
            providerEventId: model.ProviderEventId,
            calendarId: model.CalendarId,
            subject: model.Subject,
            startUtc: model.StartUtc,
            endUtc: model.EndUtc,
            isCancelled: model.IsCancelled,
            fetchedUtc: model.FetchedUtc,
            fetchError: model.FetchError,
            changeKey: model.ChangeKey,
            iCalUId: model.ICalUId
        );
    }
}