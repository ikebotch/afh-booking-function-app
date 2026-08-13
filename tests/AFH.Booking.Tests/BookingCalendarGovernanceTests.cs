using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Calendar;
using AFH.Booking.Application.Holds;
using AFH.Booking.Application.Models.Calendar.Constants;
using AFH.Booking.Domain.Bookings;

namespace AFH.Booking.Tests;

public sealed class BookingCalendarGovernanceTests
{
    private static readonly DateTime StartUtc = new(2026, 06, 08, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConfirmedBooking_WithDeletedOutlookEvent_IsRestoredAsBusyWithoutLifecycleChange()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-old");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: "evt-new");
        var holds = new StubHoldRepository(hold);
        var issues = new StubOperationalIssueRepository();
        var sut = CreateSut(holds, slot, transaction, calendar, issues);

        var result = await sut.HandleAsync(hold.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RestoredMissingEvent);
        Assert.Equal("evt-old", result.Value.PreviousEventId);
        Assert.Equal("evt-new", result.Value.EventId);
        Assert.Equal("evt-new", hold.CalendarProviderEventId);
        Assert.Equal(BookingHoldStatus.Confirmed, hold.Status);
        Assert.True(calendar.CreatedEvent);
        Assert.Equal(BookingShowAs.Busy, calendar.LastCreatedEvent?.ShowAs);
        Assert.Contains(CalendarCategoryConstants.MissingEventRestored, calendar.LastCreatedEvent?.Categories ?? []);
        Assert.Contains("AFH Booking", calendar.LastCreatedEvent?.Body ?? string.Empty);
        Assert.DoesNotContain("token=", calendar.LastCreatedEvent?.Body ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Single(issues.Added, issue => issue.Code == OutlookIssueCodes.CalendarEventMissingRestored);
    }

    [Fact]
    public async Task ConfirmedBooking_WithExistingOutlookEvent_UpdatesShowAsAndDoesNotCreateDuplicate()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-existing");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(new CalendarEventDetails { CalendarId = "evt-existing" }, createdEventId: "evt-new");
        var sut = CreateSut(new StubHoldRepository(hold), slot, transaction, calendar, new StubOperationalIssueRepository());

        var result = await sut.HandleAsync(hold.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RestoredMissingEvent);
        Assert.Equal("evt-existing", hold.CalendarProviderEventId);
        Assert.False(calendar.CreatedEvent);
        Assert.True(calendar.UpdatedEvent);
        Assert.Equal(BookingShowAs.Busy, calendar.LastUpdatedEvent?.ShowAs);
    }

    [Fact]
    public async Task ProviderNotification_WithManualCalendarEdit_CorrectsEventAndRecordsGovernanceIssue()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-existing");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(
            new CalendarEventDetails
            {
                CalendarId = "evt-existing",
                StartUtc = slot.StartUtc.AddMinutes(15),
                EndUtc = slot.EndUtc.AddMinutes(15),
                ShowAs = "Free"
            },
            createdEventId: "evt-new");
        var issues = new StubOperationalIssueRepository();
        var sut = CreateSut(new StubHoldRepository(hold), slot, transaction, calendar, issues);

        var result = await sut.HandleProviderNotificationsAsync(
            new CalendarProviderNotificationEnvelope
            {
                Value =
                [
                    new CalendarProviderNotificationItem
                    {
                        ChangeType = "updated",
                        Resource = "users/adviser.one@tenant.test/events/evt-existing",
                        ResourceData = new CalendarProviderResourceData { Id = "evt-existing" }
                    }
                ]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Corrected);
        Assert.False(calendar.CreatedEvent);
        Assert.True(calendar.UpdatedEvent);
        Assert.Equal(slot.StartUtc, calendar.LastUpdatedEvent?.StartUtc);
        Assert.Equal(slot.EndUtc, calendar.LastUpdatedEvent?.EndUtc);
        Assert.Equal(BookingShowAs.Busy, calendar.LastUpdatedEvent?.ShowAs);
        Assert.Single(issues.Added, issue => issue.Code == OutlookIssueCodes.EventTamperingDetected);
    }

    [Fact]
    public async Task ProviderNotification_WithManualCalendarDeletion_RestoresEventAndRecordsDeletionAttempt()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-old");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: "evt-new");
        var issues = new StubOperationalIssueRepository();
        var sut = CreateSut(new StubHoldRepository(hold), slot, transaction, calendar, issues);

        var result = await sut.HandleProviderNotificationsAsync(
            new CalendarProviderNotificationEnvelope
            {
                Value =
                [
                    new CalendarProviderNotificationItem
                    {
                        ChangeType = "deleted",
                        ResourceData = new CalendarProviderResourceData { Id = "evt-old" }
                    }
                ]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Restored);
        Assert.Equal("evt-new", hold.CalendarProviderEventId);
        Assert.True(calendar.CreatedEvent);
        Assert.Contains(issues.Added, issue => issue.Code == OutlookIssueCodes.DeletionAttemptDetected);
        Assert.Contains(issues.Added, issue => issue.Code == OutlookIssueCodes.CalendarEventMissingRestored);
    }

    [Fact]
    public async Task ProviderNotification_WithDuplicateManualDeletion_RestoresOnlyOnce()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-old");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: "evt-new");
        var issues = new StubOperationalIssueRepository();
        var sut = CreateSut(new StubHoldRepository(hold), slot, transaction, calendar, issues);
        var notification = new CalendarProviderNotificationItem
        {
            ChangeType = "deleted",
            ResourceData = new CalendarProviderResourceData { Id = "evt-old" }
        };

        var first = await sut.HandleProviderNotificationsAsync(
            new CalendarProviderNotificationEnvelope { Value = [notification] },
            CancellationToken.None);
        var second = await sut.HandleProviderNotificationsAsync(
            new CalendarProviderNotificationEnvelope { Value = [notification] },
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, first.Value!.Restored);
        Assert.Equal(0, second.Value!.Restored);
        Assert.Equal(1, second.Value.Ignored);
        Assert.Equal(1, calendar.CreatedCount);
        Assert.Equal("evt-new", hold.CalendarProviderEventId);
    }

    [Fact]
    public async Task ProviderNotification_WithUpdatedEventLookupMiss_FlagsOperationsAndDoesNotCreateDuplicate()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-existing");
        var slot = CreateSlot();
        var transaction = CreateTransaction();
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: "evt-new");
        var issues = new StubOperationalIssueRepository();
        var sut = CreateSut(new StubHoldRepository(hold), slot, transaction, calendar, issues);

        var result = await sut.HandleProviderNotificationsAsync(
            new CalendarProviderNotificationEnvelope
            {
                Value =
                [
                    new CalendarProviderNotificationItem
                    {
                        ChangeType = "updated",
                        ResourceData = new CalendarProviderResourceData { Id = "evt-existing" }
                    }
                ]
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.FlaggedForOperations);
        Assert.False(calendar.CreatedEvent);
        Assert.False(calendar.UpdatedEvent);
        Assert.Equal("evt-existing", hold.CalendarProviderEventId);
        Assert.Single(issues.Added, issue => issue.Code == OutlookIssueCodes.ControlledReconciliationRequired);
    }

    [Fact]
    public async Task CancelledBooking_WithDeletedOutlookEvent_IsNotRestored()
    {
        var hold = CreateHold(BookingHoldStatus.Cancelled, "evt-old");
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: "evt-new");
        var sut = CreateSut(new StubHoldRepository(hold), CreateSlot(), CreateTransaction(), calendar, new StubOperationalIssueRepository());

        var result = await sut.HandleAsync(hold.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BookingHoldStatus.Cancelled, hold.Status);
        Assert.Equal("evt-old", hold.CalendarProviderEventId);
        Assert.False(calendar.CreatedEvent);
    }

    [Fact]
    public async Task RestoreFailure_RecordsOperationalIssueAndDoesNotMutateBookingLifecycleOrProviderId()
    {
        var hold = CreateHold(BookingHoldStatus.Confirmed, "evt-old");
        var issues = new StubOperationalIssueRepository();
        var calendar = new StubCalendarGateway(existingEvent: null, createdEventId: null);
        var sut = CreateSut(new StubHoldRepository(hold), CreateSlot(), CreateTransaction(), calendar, issues);

        var result = await sut.HandleAsync(hold.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OutlookIssueCodes.CalendarEventMissingRestoreFailed, result.ErrorCode);
        Assert.Equal(BookingHoldStatus.Confirmed, hold.Status);
        Assert.Equal("evt-old", hold.CalendarProviderEventId);
        Assert.Single(issues.Added, issue => issue.Code == OutlookIssueCodes.CalendarEventMissingRestoreFailed);
    }

    private static BookingShowAsRemediationService CreateSut(
        StubHoldRepository holds,
        BookingSlot slot,
        BookingTransaction transaction,
        StubCalendarGateway calendar,
        StubOperationalIssueRepository issues)
        => new(
            holds,
            new StubSlotRepository(slot),
            new StubTransactionRepository(transaction),
            calendar,
            new HoldWindowFactory(),
            issues,
            new StubUnitOfWork());

    private static BookingHold CreateHold(BookingHoldStatus status, string? providerEventId)
        => BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "client-1",
            status,
            StartUtc.AddDays(-1),
            StartUtc.AddHours(1),
            status == BookingHoldStatus.Confirmed ? StartUtc.AddHours(-2) : null,
            null,
            status == BookingHoldStatus.Cancelled ? StartUtc.AddHours(-1) : null,
            status == BookingHoldStatus.Cancelled ? "Cancelled" : null,
            providerEventId,
            "booking-1");

    private static BookingSlot CreateSlot()
        => BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser.one@tenant.test",
            "Adviser One",
            StartUtc,
            StartUtc.AddMinutes(45),
            5,
            null,
            null,
            null,
            null,
            null,
            "Eligible",
            null,
            StartUtc.AddDays(-1));

    private static BookingTransaction CreateTransaction()
        => BookingTransaction.Rehydrate(
            "tx-1",
            "TRX-1",
            StartUtc,
            TimeSpan.FromMinutes(45),
            "Europe/London",
            true,
            "Review",
            null,
            BookingTransactionStatus.Completed,
            StartUtc.AddDays(-1),
            null);

    private sealed class StubHoldRepository(BookingHold hold) : IBookingHoldRepository
    {
        public BookingHold? Updated { get; private set; }
        public Task AddAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(hold);
        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(hold);
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<int> CountActiveOrConfirmedByAdviserAsync(string adviserId, DateTime fromUtc, DateTime toUtc, DateTime utcNow, CancellationToken ct) => Task.FromResult(0);
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(hold);
        public Task<IReadOnlyList<BookingHold>> GetAllActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingHold?>(hold);
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => Task.FromResult<BookingHold?>(hold);
        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<ActiveHoldLookupResult> GetActiveForCreateHoldAsync(string transactionId, string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult(new ActiveHoldLookupResult(null, null));
        public Task UpdateAsync(BookingHold hold, CancellationToken ct)
        {
            Updated = hold;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSlotRepository(BookingSlot slot) : IBookingSlotRepository
    {
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingSlot?>(slot);
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingSlot>>([slot]);
        public Task AddAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubTransactionRepository(BookingTransaction transaction) : IBookingTransactionRepository
    {
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(transaction);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(transaction);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(transaction);
    }

    private sealed class StubCalendarGateway(CalendarEventDetails? existingEvent, string? createdEventId) : ICalendarGateway
    {
        public bool CreatedEvent { get; private set; }
        public bool UpdatedEvent { get; private set; }
        public int CreatedCount { get; private set; }
        public BookingCalendarEvent? LastCreatedEvent { get; private set; }
        public BookingCalendarEvent? LastUpdatedEvent { get; private set; }

        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
        {
            CreatedEvent = true;
            CreatedCount++;
            LastCreatedEvent = ev;
            return Task.FromResult(createdEventId);
        }

        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct) => Task.CompletedTask;

        public Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
        {
            UpdatedEvent = true;
            LastUpdatedEvent = ev;
            return Task.FromResult<string?>(ev.EventId);
        }

        public Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default) => Task.FromResult(existingEvent);

        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(string userId, DateTime startUtc, DateTime endUtc, string timezone, string? freshnessMode, CancellationToken ct)
            => Task.FromResult(new AdviserAvailabilityResult { IsFree = true });
    }

    private sealed class StubOperationalIssueRepository : IOperationalIssueRepository
    {
        public List<OperationalIssueRecord> Added { get; } = [];
        public Task AddAsync(OperationalIssueRecord record, CancellationToken ct)
        {
            Added.Add(record);
            return Task.CompletedTask;
        }

        public Task<OperationalIssueRecord?> GetLatestAsync(string adviserId, string providerEventId, string code, CancellationToken ct) => Task.FromResult<OperationalIssueRecord?>(null);
        public Task<int> CountRecentAsync(string adviserId, string code, DateTime sinceUtc, CancellationToken ct) => Task.FromResult(0);
        public Task UpdateAsync(OperationalIssueRecord record, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
