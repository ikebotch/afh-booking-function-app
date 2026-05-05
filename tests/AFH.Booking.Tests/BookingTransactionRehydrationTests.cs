using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Common;
using AFH.Booking.Domain.Transactions;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class BookingTransactionRehydrationTests
{
    [Fact]
    public async Task BookingTransactionRepository_GetForUpdateAsync_RehydratesCompletedTransactionWithPersistedSlots()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;

        SeedCompletedTransactionGraph(db, now);

        var repository = new BookingTransactionRepository(db);

        var transaction = await repository.GetForUpdateAsync("tx-1", CancellationToken.None);

        Assert.NotNull(transaction);
        Assert.Equal(BookingTransactionStatus.Completed, transaction!.Status);
        Assert.Single(transaction.Slots);
        Assert.Equal("slot-1", transaction.Slots[0].Id);
    }

    [Fact]
    public async Task ConfirmBookingHandler_HandleAsync_DoesNotBlowUpWhenCompletedTransactionIsRehydrated()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;

        SeedCompletedTransactionGraph(db, now);

        var sut = new ConfirmBookingHandler(
            new BookingHoldRepository(db),
            new BookingSlotRepository(db),
            new BookingTransactionRepository(db),
            new UnitOfWork(db),
            new StubClock(now),
            new StubCalendarGateway(),
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(),
            new StubLifecycleAuditService(),
            new StubNotificationService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = "hold-1" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Confirmed", result.Value!.Status);
    }

    [Fact]
    public void BookingTransaction_AddSlot_StillThrowsForCompletedTransactions()
    {
        var now = DateTime.UtcNow;
        var transaction = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: false,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Completed,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddHours(2));

        var slot = BookingSlot.Rehydrate(
            id: "slot-2",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 10,
            companyBufferMinutes: 30,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now);

        var ex = Assert.Throws<DomainException>(() => transaction.AddSlot(slot));

        Assert.Equal("Transaction is not open.", ex.Message);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }

    private static void SeedCompletedTransactionGraph(BookingDbContext db, DateTime now)
    {
        var transaction = new BookingTransactionModel
        {
            Id = "tx-1",
            TransactionRef = "TRX-1",
            ProposedStartUtc = now.AddHours(1),
            DurationMinutes = 60,
            Timezone = "UTC",
            IsRemote = false,
            MeetingType = "Review",
            LocationRef = null,
            Status = (int)BookingTransactionStatus.Completed,
            CreatedUtc = now.AddHours(-1),
            ExpiresUtc = now.AddHours(2),
            RowVersion = [1]
        };

        var slot = new BookingSlotModel
        {
            Id = "slot-1",
            TransactionId = transaction.Id,
            AdviserId = "adv-1",
            AdviserName = "Adviser One",
            StartUtc = now.AddHours(1),
            EndUtc = now.AddHours(2),
            Score = 5,
            LocationRef = null,
            TravelMinutes = 10,
            CompanyBufferMinutes = 30,
            DistanceMiles = null,
            TravelStatus = null,
            TravelMessage = null,
            CreatedUtc = now.AddMinutes(-30),
            Transaction = transaction
        };

        var hold = new BookingHoldModel
        {
            Id = "hold-1",
            UserId = "user-1",
            SlotId = slot.Id,
            Slot = slot,
            Status = HoldStatus.Active,
            CreatedUtc = now.AddMinutes(-20),
            HoldExpiresUtc = now.AddMinutes(20),
            ConfirmedUtc = null,
            ReleasedUtc = null,
            CancelledUtc = null,
            CancelReason = null,
            CalendarProviderEventId = null,
            RowVersion = [1]
        };

        transaction.Slots.Add(slot);
        slot.Hold = hold;

        db.BookingTransactions.Add(transaction);
        db.BookingSlots.Add(slot);
        db.Holds.Add(hold);
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private sealed class StubClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StubProfiles(string adviserId, string mailboxUserId) : IAdviserProfileProjectionRepository
    {
        private readonly AdviserProfileProjectionRecord _record = new()
        {
            AdviserId = adviserId,
            DisplayName = adviserId,
            MailboxUserId = mailboxUserId,
            IsActive = true
        };

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
            => Task.FromResult(string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
    }

    private sealed class StubMeetingLinkFactory : IMeetingLinkFactory
    {
        public Task<string?> CreateJoinLinkAsync(string holdId, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private sealed class StubConflictService : IBookingConflictService
    {
        public Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(BookingHold hold, BookingSlot slot, BookingTransaction transaction, string calendarUserId, CancellationToken ct)
            => Task.FromResult(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>()));
    }

    private sealed class StubLifecycleAuditService : ILifecycleAuditService
    {
        public Task<string> RecordEventAsync(LifecycleAuditEntry entry, CancellationToken ct) => Task.FromResult("event-1");
        public Task RecordStepAsync(LifecycleAuditStepEntry step, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task<NotificationDispatchResponse> SendBookingNotificationAsync(NotificationDispatchRequest request, CancellationToken ct)
            => Task.FromResult(new NotificationDispatchResponse
            {
                DispatchId = "dispatch-1",
                BookingId = request.BookingId,
                EventType = request.EventType,
                SmsRequested = request.SendSms,
                EmailRequested = request.SendEmail,
                SmsStatus = "Skipped",
                EmailStatus = "Skipped",
                ProviderMessageId = "provider-1",
                CreatedUtc = DateTime.UtcNow
            });
    }

    private sealed class StubCalendarGateway : ICalendarGateway
    {
        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
            => Task.FromResult<string?>(null);

        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct)
            => Task.CompletedTask;

        public Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
            => Task.FromResult<string?>(null);

        public Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default)
            => Task.FromResult<CalendarEventDetails?>(null);

        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(string userId, DateTime startUtc, DateTime endUtc, string timezone, string? freshnessMode, CancellationToken ct)
            => Task.FromResult(new AdviserAvailabilityResult { IsFree = true, Conflicts = [] });
    }
}
