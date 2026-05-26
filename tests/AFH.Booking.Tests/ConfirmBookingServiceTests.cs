using AFH.Booking.Application.EmailTemplates;

using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Governance;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Tests;

public class ConfirmBookingServiceTests
{
    [Fact]
    public async Task HandleAsync_ReturnsHoldCancelledCode_WhenHoldWasCancelled()
    {
        var hold = BookingHold.Rehydrate(
            id: "hold-1",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Cancelled,
            createdUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: DateTime.UtcNow.AddMinutes(-1),
            cancelReason: "User cancelled",
            providerEventId: null, null);

        var sut = NewService(hold);

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.HoldCancelled, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsHoldExpiredCode_WhenHoldHasExpired()
    {
        var hold = BookingHold.Rehydrate(
            id: "hold-2",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Active,
            createdUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddMinutes(-1),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: null, null);

        var sut = NewService(hold);

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.HoldExpired, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAlreadyConfirmedCode_WhenHoldWasAlreadyConfirmed()
    {
        var hold = BookingHold.Rehydrate(
            id: "hold-3",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Confirmed,
            createdUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddMinutes(10),
            confirmedUtc: DateTime.UtcNow.AddMinutes(-2),
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: null, null);

        var sut = NewService(hold);

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.HoldAlreadyConfirmed, result.ErrorCode);
    }

    [Fact]
    public async Task HandleAsync_BlocksCalendarMutation_WhenConflictDetected()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = BookingHold.Rehydrate(
            id: "hold-4",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-10),
            expiresUtc: now.AddMinutes(10),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: "evt-1", null);

        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 15,
            companyBufferMinutes: 30,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now.AddMinutes(-20));

        var tx = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: false,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddDays(1));

        var calendar = new StubCalendarGateway();
        var profiles = new StubProfiles("adv-1", "adviser.one@tenant.com");
        var conflicts = new StubConflictService(new BookingConflictCheckResult(
            true,
            Errors.BookingConflictDoubleBooked,
            "Adviser already has a conflicting event.",
            [new BookingConflictDetail(Errors.BookingConflictDoubleBooked, "conflict")]));
        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            profiles,
            new StubMeetingLinkFactory(),
            conflicts,
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictDoubleBooked, result.ErrorCode);
        Assert.False(calendar.UpdateCalled);
        Assert.Equal(1, profiles.ResolveCallCount);
        Assert.Equal("adviser.one@tenant.com", conflicts.LastCalendarUserId);
    }

    [Fact]
    public async Task HandleAsync_UsesPlainTextCalendarDescription_WhenUpdatingConfirmedEvent()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = BookingHold.Rehydrate(
            id: "hold-5",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-10),
            expiresUtc: now.AddMinutes(10),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: "evt-1", null);

        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 0,
            companyBufferMinutes: 0,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now.AddMinutes(-20));

        var tx = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: true,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddDays(1));

        var calendar = new StubCalendarGateway();
        var profiles = new StubProfiles("adv-1", "adviser.one@tenant.com");
        var meetingLinks = new StubMeetingLinkFactory();
        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            profiles,
            meetingLinks,
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(calendar.UpdateCalled);
        Assert.Equal("adviser.one@tenant.com", calendar.LastUpdatedUserId);
        Assert.DoesNotContain("<html", calendar.LastUpdatedBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://meeting.example", calendar.LastUpdatedBody ?? string.Empty);
        Assert.Equal(1, profiles.ResolveCallCount);
        Assert.Equal(1, meetingLinks.CallCount);
    }

    [Fact]
    public async Task HandleAsync_RecordsBookedLifecycleEvent_AndReturnsTransactionReference()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = ActiveHold(now, providerEventId: null);
        var slot = InPersonSlot(now);
        var tx = InPersonTransaction(now);
        var audit = new StubLifecycleAuditService();
        var notifications = new StubNotificationService();
        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            new StubCalendarGateway(),
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())),
            new StubRouteTimeGuard(),
            audit,
            notifications, new StubHoldWindowFactory(), new StubBookingTokenService("client-token-1"));

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("tx-1", result.Value.TransactionId);
        Assert.Equal("TRX-1", result.Value.TransactionRef);
        Assert.Equal(LifecycleEventTypes.Booked, result.Value.LifecycleState);
        Assert.Equal(BookingHoldStatus.Confirmed.ToString(), result.Value.Status);
        Assert.Equal(LifecycleEventTypes.Booked, audit.LastEvent?.EventType);
        Assert.Equal(tx.Id, audit.LastEvent?.TransactionId);
        Assert.Equal(LifecycleActors.Client, audit.LastEvent?.ActorType);
        Assert.Equal(LifecycleEventTypes.Booked, notifications.LastRequest?.EventType);
        Assert.Equal("client-token-1", notifications.LastRequest?.ClientSelfServiceToken);
        Assert.Equal(BookingTransactionStatus.Completed, tx.Status);
    }

    [Fact]
    public async Task HandleAsync_ResolvesCalendarUserIdOnlyAfterTransactionLookupCompletes()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = BookingHold.Rehydrate(
            id: "hold-6",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-10),
            expiresUtc: now.AddMinutes(10),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: null, null);

        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 0,
            companyBufferMinutes: 0,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now.AddMinutes(-20));

        var tx = BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: true,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddDays(1));

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var txRepo = new SequencedTransactionRepository(tx, gate.Task);
        var profiles = new SequencedProfiles("adv-1", "adviser.one@tenant.com");
        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            txRepo,
            new StubUnitOfWork(),
            new StubClock(now),
            new StubCalendarGateway(),
            profiles,
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var handleTask = sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        await Task.Yield();

        Assert.False(handleTask.IsCompleted);
        Assert.False(profiles.GetAsyncStarted);

        gate.SetResult();

        var result = await handleTask;

        Assert.True(result.IsSuccess);
        Assert.True(profiles.GetAsyncStarted);
    }

    [Fact]
    public async Task HandleAsync_Succeeds_WhenOnlyConflictIsCurrentHoldProviderEvent()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = ActiveHold(now, providerEventId: "evt-self");
        var slot = InPersonSlot(now);
        var tx = InPersonTransaction(now);
        var calendar = new StubCalendarGateway(new AdviserAvailabilityResult
        {
            IsFree = false,
            MailboxUnavailable = false,
            StatusMessage = "Conflicts found",
            Conflicts =
            [
                new CalendarConflictBlock
                {
                    StartUtc = slot.StartUtc.AddMinutes(5),
                    EndUtc = slot.StartUtc.AddMinutes(25),
                    Subject = "Current hold",
                    ProviderEventId = "evt-self"
                }
            ]
        });

        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new BookingConflictService(calendar, new StubOperationalIssueRepository(), new StubUnitOfWork(), new StubClock(now)),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(calendar.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_Fails_WhenConflictIsDifferentProviderEvent()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = ActiveHold(now, providerEventId: "evt-self");
        var slot = InPersonSlot(now);
        var tx = InPersonTransaction(now);
        var calendar = new StubCalendarGateway(new AdviserAvailabilityResult
        {
            IsFree = false,
            MailboxUnavailable = false,
            StatusMessage = "Conflicts found",
            Conflicts =
            [
                new CalendarConflictBlock
                {
                    StartUtc = slot.StartUtc.AddMinutes(5),
                    EndUtc = slot.StartUtc.AddMinutes(25),
                    Subject = "Different event",
                    ProviderEventId = "evt-other"
                }
            ]
        });

        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new BookingConflictService(calendar, new StubOperationalIssueRepository(), new StubUnitOfWork(), new StubClock(now)),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictDoubleBooked, result.ErrorCode);
        Assert.False(calendar.UpdateCalled);
    }

    [Fact]
    public async Task HandleAsync_Fails_WhenConflictExistsAndHoldHasNoProviderEventId()
    {
        var now = DateTime.UtcNow.AddHours(1);
        var hold = ActiveHold(now, providerEventId: null);
        var slot = InPersonSlot(now);
        var tx = InPersonTransaction(now);
        var calendar = new StubCalendarGateway(new AdviserAvailabilityResult
        {
            IsFree = false,
            MailboxUnavailable = false,
            StatusMessage = "Conflicts found",
            Conflicts =
            [
                new CalendarConflictBlock
                {
                    StartUtc = slot.StartUtc.AddMinutes(5),
                    EndUtc = slot.StartUtc.AddMinutes(25),
                    Subject = "Different event",
                    ProviderEventId = "evt-other"
                }
            ]
        });

        var sut = new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new BookingConflictService(calendar, new StubOperationalIssueRepository(), new StubUnitOfWork(), new StubClock(now)),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictDoubleBooked, result.ErrorCode);
        Assert.False(calendar.UpdateCalled);
    }

    private static ConfirmBookingService NewService(BookingHold hold)
    {
        return new ConfirmBookingService(
            new StubHoldRepository(hold),
            new StubSlotRepository(),
            new StubTransactionRepository(),
            new StubUnitOfWork(),
            new StubClock(DateTime.UtcNow),
            new StubCalendarGateway(),
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())),
            new StubRouteTimeGuard(),
            new StubLifecycleAuditService(),
            new StubNotificationService(), new StubHoldWindowFactory(), new StubBookingTokenService());
    }

    private static BookingHold ActiveHold(DateTime now, string? providerEventId)
        => BookingHold.Rehydrate(
            id: "hold-test",
            slotId: "slot-1",
            userid: "user-1",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-10),
            expiresUtc: now.AddMinutes(10),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: providerEventId, null);

    private static BookingSlot InPersonSlot(DateTime now)
        => BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 15,
            companyBufferMinutes: 30,
            distanceMiles: null,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now.AddMinutes(-20));

    private static BookingTransaction InPersonTransaction(DateTime now)
        => BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: false,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddDays(1));

    private sealed class StubHoldRepository : IBookingHoldRepository
    {
        private readonly BookingHold _hold;

        public StubHoldRepository(BookingHold hold) => _hold = hold;
        public Task AddAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<ActiveHoldLookupResult> GetActiveForCreateHoldAsync(string transactionId, string slotId, DateTime utcNow, CancellationToken ct)
            => Task.FromResult(new ActiveHoldLookupResult(null, null));
        public Task UpdateAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<int> CountActiveOrConfirmedByAdviserAsync(string adviserId, DateTime fromUtc, DateTime toUtc, DateTime utcNow, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<BookingHold>> GetAllActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
    }

    private sealed class StubSlotRepository : IBookingSlotRepository
    {
        private readonly BookingSlot? _slot;

        public StubSlotRepository(BookingSlot? slot = null) => _slot = slot;
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct) => Task.FromResult(_slot);
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingSlot>>([]);
        public Task AddAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubRouteTimeGuard : ISelectedSlotRouteTimeGuard
    {
        private readonly SelectedSlotRouteTimeGuardResult _result;

        public StubRouteTimeGuard(SelectedSlotRouteTimeGuardResult? result = null)
        {
            _result = result ?? new SelectedSlotRouteTimeGuardResult(
                true,
                false,
                null,
                null,
                null,
                null);
        }

        public int CallCount { get; private set; }

        public Task<SelectedSlotRouteTimeGuardResult> EvaluateAsync(
            BookingSlot slot,
            BookingTransaction transaction,
            string holdId,
            CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubTransactionRepository : IBookingTransactionRepository
    {
        private readonly BookingTransaction? _transaction;
        public StubTransactionRepository(BookingTransaction? transaction = null) => _transaction = transaction;
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult(_transaction);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult(_transaction);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => Task.FromResult(_transaction);
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class StubProfiles : IAdviserProfileProjectionRepository
    {
        private readonly AdviserProfileProjectionRecord _record;

        public StubProfiles(string adviserId, string mailboxUserId)
        {
            _record = new AdviserProfileProjectionRecord
            {
                AdviserId = adviserId,
                DisplayName = adviserId,
                MailboxUserId = mailboxUserId,
                IsActive = true
            };
        }

        public int ResolveCallCount { get; private set; }

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
        {
            ResolveCallCount++;
            return Task.FromResult(string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
        }
    }

    private sealed class SequencedTransactionRepository : IBookingTransactionRepository
    {
        private readonly BookingTransaction _transaction;
        private readonly Task _gate;

        public SequencedTransactionRepository(BookingTransaction transaction, Task gate)
        {
            _transaction = transaction;
            _gate = gate;
        }

        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_transaction);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_transaction);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;

        public async Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            return _transaction;
        }
    }

    private sealed class SequencedProfiles : IAdviserProfileProjectionRepository
    {
        private readonly AdviserProfileProjectionRecord _record;

        public SequencedProfiles(string adviserId, string mailboxUserId)
        {
            _record = new AdviserProfileProjectionRecord
            {
                AdviserId = adviserId,
                DisplayName = adviserId,
                MailboxUserId = mailboxUserId,
                IsActive = true
            };
        }

        public bool GetAsyncStarted { get; private set; }

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);

        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
        {
            GetAsyncStarted = true;
            return Task.FromResult(string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
        }
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class StubCalendarGateway : ICalendarGateway
    {
        private readonly AdviserAvailabilityResult _availabilityResult;

        public StubCalendarGateway(AdviserAvailabilityResult? availabilityResult = null)
        {
            _availabilityResult = availabilityResult ?? new AdviserAvailabilityResult();
        }

        public bool UpdateCalled { get; private set; }
        public string? LastUpdatedUserId { get; private set; }
        public string? LastUpdatedBody { get; private set; }
        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
        {
            UpdateCalled = true;
            LastUpdatedUserId = ev.UserId;
            LastUpdatedBody = ev.Body;
            return Task.FromResult<string?>(null);
        }
        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct) => Task.CompletedTask;
        public Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default) => Task.FromResult<CalendarEventDetails?>(null);
        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(string userId, DateTime startUtc, DateTime endUtc, string timezone, string? freshnessMode, CancellationToken ct) => Task.FromResult(_availabilityResult);
    }

    private sealed class StubMeetingLinkFactory : IMeetingLinkFactory
    {
        public int CallCount { get; private set; }

        public Task<string?> CreateJoinLinkAsync(string bookingId, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult<string?>("https://meeting.example");
        }
    }

    private sealed class StubConflictService : IBookingConflictService
    {
        private readonly BookingConflictCheckResult _result;

        public StubConflictService(BookingConflictCheckResult result) => _result = result;

        public string? LastCalendarUserId { get; private set; }

        public Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(BookingHold hold, BookingSlot slot, BookingTransaction transaction, string calendarUserId, CancellationToken ct)
        {
            LastCalendarUserId = calendarUserId;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubLifecycleAuditService : ILifecycleAuditService
    {
        public LifecycleAuditEntry? LastEvent { get; private set; }
        public List<LifecycleAuditStepEntry> Steps { get; } = [];

        public Task<string> RecordEventAsync(LifecycleAuditEntry entry, CancellationToken ct)
        {
            LastEvent = entry;
            return Task.FromResult("lifecycle-event-1");
        }

        public Task RecordStepAsync(LifecycleAuditStepEntry step, CancellationToken ct)
        {
            Steps.Add(step);
            return Task.CompletedTask;
        }
    }

    private sealed class StubNotificationService : INotificationService
    {
        public NotificationDispatchRequest? LastRequest { get; private set; }

        public Task<NotificationDispatchResponse> SendBookingNotificationAsync(NotificationDispatchRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new NotificationDispatchResponse
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
    }

    private sealed class StubBookingTokenService(string token = "client-token") : IBookingTokenService
    {
        public Task<Result<string>> GenerateClientAccessTokenAsync(string bookingId, CancellationToken ct)
            => Task.FromResult(Result<string>.Ok(token));
    }

    private sealed class StubOperationalIssueRepository : IOperationalIssueRepository
    {
        public Task AddAsync(OperationalIssueRecord record, CancellationToken ct) => Task.CompletedTask;
        public Task<OperationalIssueRecord?> GetLatestAsync(string adviserId, string providerEventId, string code, CancellationToken ct)
            => Task.FromResult<OperationalIssueRecord?>(null);
        public Task<int> CountRecentAsync(string adviserId, string code, DateTime sinceUtc, CancellationToken ct) => Task.FromResult(0);
        public Task UpdateAsync(OperationalIssueRecord record, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubHoldWindowFactory : IHoldWindowFactory
    {
        public HoldWindows Create(BookingSlot slot, BookingTransaction transaction)
            => new HoldWindows(slot.StartUtc, slot.EndUtc, 0, 0, false);
    }
}
