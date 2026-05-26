using AFH.Booking.Application.EmailTemplates;
using Moq;

﻿using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Common;
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
    public async Task ConfirmBookingService_HandleAsync_DoesNotBlowUpWhenCompletedTransactionIsRehydrated()
    {
        // This test verifies that rehydrating a completed transaction via EF does not
        // throw an exception inside the service (the transaction is already Completed,
        // so MarkCompleted is a no-op and the service should short-circuit to success).
        var db = CreateDbContext();
        var now = DateTime.UtcNow;

        SeedCompletedTransactionGraph(db, now);

        // Use real EF-backed repositories so the service can actually find the seeded hold/slot/tx
        var holdRepo = new BookingHoldRepository(db);
        var slotRepo = new BookingSlotRepository(db);
        var txRepo = new BookingTransactionRepository(db);

        var holdWindowFactory = new Mock<IHoldWindowFactory>();
        holdWindowFactory
            .Setup(f => f.Create(It.IsAny<BookingSlot>(), It.IsAny<BookingTransaction>()))
            .Returns(new HoldWindows(now.AddMinutes(-45), now.AddMinutes(60), 0, 0, false));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(now);

        var calendarGateway = new Mock<ICalendarGateway>();
        calendarGateway
            .Setup(c => c.UpdateBookingEventAsync(It.IsAny<BookingCalendarEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        profiles
            .Setup(p => p.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdviserProfileProjectionRecord { AdviserId = "adv-1", DisplayName = "Adviser One", MailboxUserId = "user-1" });

        var conflicts = new Mock<IBookingConflictService>();
        conflicts
            .Setup(c => c.EvaluateConfirmationConflictsAsync(
                It.IsAny<BookingHold>(), It.IsAny<BookingSlot>(), It.IsAny<BookingTransaction>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>()));

        var routeTimeGuard = new Mock<ISelectedSlotRouteTimeGuard>();
        routeTimeGuard
            .Setup(g => g.EvaluateAsync(
                It.IsAny<BookingSlot>(),
                It.IsAny<BookingTransaction>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelectedSlotRouteTimeGuardResult(true, false, null, null, null, null));

        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(a => a.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("event-1");
        audit.Setup(a => a.RecordStepAsync(It.IsAny<LifecycleAuditStepEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.SendBookingNotificationAsync(It.IsAny<NotificationDispatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResponse
            {
                DispatchId = "d-1", BookingId = "hold-1", EventType = "Confirmed",
                SmsRequested = false, EmailRequested = false, SmsStatus = "Skipped",
                EmailStatus = "Skipped", ProviderMessageId = "p-1", CreatedUtc = now
            });

        var tokenService = new Mock<IBookingTokenService>();
        tokenService
            .Setup(t => t.GenerateClientAccessTokenAsync("hold-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Ok("client-token"));

        var sut = new ConfirmBookingService(
            holdRepo,
            slotRepo,
            txRepo,
            new Mock<IUnitOfWork>().Object,
            clock.Object,
            calendarGateway.Object,
            profiles.Object,
            new Mock<IMeetingLinkFactory>().Object,
            conflicts.Object,
            routeTimeGuard.Object,
            audit.Object,
            notifications.Object,
            holdWindowFactory.Object,
            tokenService.Object
        );

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
