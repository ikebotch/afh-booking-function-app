using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

public sealed class CalendarSubscriptionRepository : ICalendarSubscriptionRepository
{
    private readonly BookingDbContext _db;

    public CalendarSubscriptionRepository(BookingDbContext db)
        => _db = db;

    public async Task<CalendarSubscription?> GetBySubscriptionIdAsync(
        string subscriptionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return null;

        var model = await _db.Set<CalendarSubscriptionModel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriptionId == subscriptionId, ct);

        return model is null ? null : MapToDomain(model);
    }

    public async Task UpsertAsync(
        CalendarSubscription subscription,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subscription.SubscriptionId))
            throw new InvalidOperationException("SubscriptionId is required.");

        var existing = await _db.Set<CalendarSubscriptionModel>()
            .FirstOrDefaultAsync(
                x => x.SubscriptionId == subscription.SubscriptionId,
                ct);

        if (existing is null)
        {
            await _db.Set<CalendarSubscriptionModel>()
                .AddAsync(MapToModel(subscription), ct);
            return;
        }

        existing.UserId = subscription.UserId;
        existing.Resource = subscription.Resource;
        existing.NotificationUrl = subscription.NotificationUrl;
        existing.ClientState = subscription.ClientState;
        existing.ExpirationUtc = subscription.ExpirationUtc;
        existing.UpdatedUtc = subscription.UpdatedUtc;
    }

    private static CalendarSubscriptionModel MapToModel(CalendarSubscription s)
        => new()
        {
            Id = s.Id,
            SubscriptionId = s.SubscriptionId,
            UserId = s.UserId,
            Resource = s.Resource,
            NotificationUrl = s.NotificationUrl,
            ClientState = s.ClientState,
            ExpirationUtc = s.ExpirationUtc,
            CreatedUtc = s.CreatedUtc,
            UpdatedUtc = s.UpdatedUtc
        
        };

    private static CalendarSubscription MapToDomain(CalendarSubscriptionModel m)
        => CalendarSubscription.Rehydrate(
            id: m.Id,
            subscriptionId: m.SubscriptionId, 
            userId: m.UserId,
            resource: m.Resource,
            notificationUrl: m.NotificationUrl,
            clientState: m.ClientState,
            expirationUtc: m.ExpirationUtc,
            createdUtc: m.CreatedUtc,
            updatedUtc: m.UpdatedUtc
        );
}