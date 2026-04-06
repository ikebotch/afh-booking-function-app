using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Transactions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public class ConfirmBookingHandlerTests
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
            providerEventId: null);

        var sut = NewHandler(hold);

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
            providerEventId: null);

        var sut = NewHandler(hold);

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
            providerEventId: null);

        var sut = NewHandler(hold);

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
            providerEventId: "evt-1");

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
        var sut = new ConfirmBookingHandler(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(
                true,
                Errors.BookingConflictDoubleBooked,
                "Adviser already has a conflicting event.",
                [new BookingConflictDetail(Errors.BookingConflictDoubleBooked, "conflict")])));

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictDoubleBooked, result.ErrorCode);
        Assert.False(calendar.UpdateCalled);
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
            providerEventId: "evt-1");

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
        var sut = new ConfirmBookingHandler(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubUnitOfWork(),
            new StubClock(now),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())));

        var result = await sut.HandleAsync(new ConfirmBookingCommand { HoldId = hold.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(calendar.UpdateCalled);
        Assert.Equal("adviser.one@tenant.com", calendar.LastUpdatedUserId);
        Assert.DoesNotContain("<html", calendar.LastUpdatedBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://meeting.example", calendar.LastUpdatedBody ?? string.Empty);
    }

    private static ConfirmBookingHandler NewHandler(BookingHold hold)
    {
        return new ConfirmBookingHandler(
            new StubHoldRepository(hold),
            new StubSlotRepository(),
            new StubTransactionRepository(),
            new StubUnitOfWork(),
            new StubClock(DateTime.UtcNow),
            new StubCalendarGateway(),
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubMeetingLinkFactory(),
            new StubConflictService(new BookingConflictCheckResult(false, null, null, Array.Empty<BookingConflictDetail>())));
    }

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
    }

    private sealed class StubSlotRepository : IBookingSlotRepository
    {
        private readonly BookingSlot? _slot;

        public StubSlotRepository(BookingSlot? slot = null) => _slot = slot;
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct) => Task.FromResult(_slot);
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingSlot>>([]);
        public Task AddAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
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

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([_record]);
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct) => Task.FromResult(string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class StubCalendarGateway : ICalendarGateway
    {
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
        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(string userId, DateTime startUtc, DateTime endUtc, string timezone, string? freshnessMode, CancellationToken ct) => Task.FromResult(new AdviserAvailabilityResult());
    }

    private sealed class StubMeetingLinkFactory : IMeetingLinkFactory
    {
        public Task<string?> CreateJoinLinkAsync(string bookingId, CancellationToken ct) => Task.FromResult<string?>("https://meeting.example");
    }

    private sealed class StubConflictService : IBookingConflictService
    {
        private readonly BookingConflictCheckResult _result;

        public StubConflictService(BookingConflictCheckResult result) => _result = result;

        public Task<BookingConflictCheckResult> EvaluateConfirmationConflictsAsync(BookingHold hold, BookingSlot slot, BookingTransaction transaction, CancellationToken ct)
            => Task.FromResult(_result);
    }
}
