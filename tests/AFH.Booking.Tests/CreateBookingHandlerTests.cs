using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Client;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class CreateBookingHandlerTests
{
    [Fact]
    public async Task HandleAsync_SameTransactionRehold_CancelsExistingHold_ChecksFreshAvailability_AndCreatesNewHold()
    {
        var now = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now);
        var oldSlot = CreateSlot("slot-old", transaction.Id, now.AddHours(2), now.AddHours(3), travelMinutes: 10, companyBufferMinutes: 20);
        var newSlot = CreateSlot("slot-new", transaction.Id, now.AddHours(4), now.AddHours(5), travelMinutes: 15, companyBufferMinutes: 30);
        var oldHold = BookingHold.Rehydrate(
            id: "hold-old",
            slotId: oldSlot.Id,
            userid: "adviser.one@tenant.com",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-2),
            expiresUtc: now.AddMinutes(2),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: "evt-old");

        var recorder = new EventRecorder();
        var holdRepo = new TrackingHoldRepository(oldHold, recorder);
        var calendar = new TrackingCalendarGateway(recorder);
        var profiles = new StubProfiles("adv-1", "adviser.one@tenant.com");
        var sut = new CreateBookingHandler(
            new StubTransactionRepository(transaction),
            new StubSlotRepository(newSlot),
            holdRepo,
            new StubUnitOfWork(),
            calendar,
            profiles,
            new StubClientDirectory(),
            new StubClock(now),
            NullLogger<CreateBookingHandler>.Instance);

        var result = await sut.HandleAsync(new CreateHoldCommand
        {
            SlotId = newSlot.Id,
            TransactionRef = transaction.TransactionRef
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "cancel-calendar", "update-old-hold", "check-availability", "add-new-hold", "create-calendar" },
            recorder.Events.Select(x => x.Name).ToArray());
        Assert.Equal(BookingHoldStatus.Cancelled, oldHold.Status);
        Assert.Equal("Superseded by a new hold attempt.", oldHold.CancelReason);
        Assert.NotNull(holdRepo.AddedHold);
        Assert.NotEqual(oldHold.Id, holdRepo.AddedHold!.Id);
        Assert.Equal("evt-old", calendar.CancelledEventId);
        Assert.Equal("adviser.one@tenant.com", calendar.LastAvailabilityUserId);
        Assert.Equal("adviser.one@tenant.com", calendar.LastCreatedUserId);
        Assert.Equal("ForceRefresh", calendar.LastFreshnessMode);
        Assert.DoesNotContain("<html", calendar.LastCreatedBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, holdRepo.ActiveLookupCallCount);
        Assert.Equal(1, profiles.ResolveCallCount);
    }

    [Fact]
    public async Task HandleAsync_UsesEffectiveHoldWindow_ForFreshAvailabilityValidation()
    {
        var now = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now);
        var slot = CreateSlot("slot-1", transaction.Id, now.AddHours(2), now.AddHours(3), travelMinutes: 15, companyBufferMinutes: 30);

        var recorder = new EventRecorder();
        var holdRepo = new TrackingHoldRepository(existingActiveHold: null, recorder);
        var calendar = new TrackingCalendarGateway(recorder);
        var profiles = new StubProfiles("adv-1", "adviser.one@tenant.com");
        var sut = new CreateBookingHandler(
            new StubTransactionRepository(transaction),
            new StubSlotRepository(slot),
            holdRepo,
            new StubUnitOfWork(),
            calendar,
            profiles,
            new StubClientDirectory(),
            new StubClock(now),
            NullLogger<CreateBookingHandler>.Instance);

        var result = await sut.HandleAsync(new CreateHoldCommand
        {
            SlotId = slot.Id,
            TransactionRef = transaction.TransactionRef
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("adviser.one@tenant.com", calendar.LastAvailabilityUserId);
        Assert.Equal(slot.StartUtc.AddMinutes(-45), calendar.LastAvailabilityStartUtc);
        Assert.Equal(slot.EndUtc.AddMinutes(30), calendar.LastAvailabilityEndUtc);
        Assert.Equal("ForceRefresh", calendar.LastFreshnessMode);
        Assert.Equal(1, holdRepo.ActiveLookupCallCount);
        Assert.Equal(1, profiles.ResolveCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenFreshAvailabilityFails_DoesNotCreateReplacementHold()
    {
        var now = new DateTime(2026, 03, 25, 10, 0, 0, DateTimeKind.Utc);
        var transaction = CreateTransaction(now);
        var oldSlot = CreateSlot("slot-old", transaction.Id, now.AddHours(1), now.AddHours(2), travelMinutes: 0, companyBufferMinutes: 30);
        var newSlot = CreateSlot("slot-new", transaction.Id, now.AddHours(3), now.AddHours(4), travelMinutes: 15, companyBufferMinutes: 30);
        var oldHold = BookingHold.Rehydrate(
            id: "hold-old",
            slotId: oldSlot.Id,
            userid: "adviser.one@tenant.com",
            status: BookingHoldStatus.Active,
            createdUtc: now.AddMinutes(-2),
            expiresUtc: now.AddMinutes(2),
            confirmedUtc: null,
            releasedUtc: null,
            cancelledUtc: null,
            cancelReason: null,
            providerEventId: "evt-old");

        var recorder = new EventRecorder();
        var holdRepo = new TrackingHoldRepository(oldHold, recorder);
        var calendar = new TrackingCalendarGateway(recorder)
        {
            AvailabilityToReturn = new AdviserAvailabilityResult
            {
                IsFree = false,
                MailboxUnavailable = false,
                StatusMessage = "Conflicts found",
                Conflicts = [new CalendarConflictBlock
                {
                    StartUtc = newSlot.StartUtc,
                    EndUtc = newSlot.EndUtc,
                    Subject = "Busy"
                }]
            }
        };

        var sut = new CreateBookingHandler(
            new StubTransactionRepository(transaction),
            new StubSlotRepository(newSlot),
            holdRepo,
            new StubUnitOfWork(),
            calendar,
            new StubProfiles("adv-1", "adviser.one@tenant.com"),
            new StubClientDirectory(),
            new StubClock(now),
            NullLogger<CreateBookingHandler>.Instance);

        var result = await sut.HandleAsync(new CreateHoldCommand
        {
            SlotId = newSlot.Id,
            TransactionRef = transaction.TransactionRef
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictDoubleBooked, result.ErrorCode);
        Assert.Equal(BookingHoldStatus.Cancelled, oldHold.Status);
        Assert.Null(holdRepo.AddedHold);
        Assert.False(calendar.CreatedBookingEvent);
        Assert.Equal(1, holdRepo.ActiveLookupCallCount);
    }

    private static BookingTransaction CreateTransaction(DateTime now) =>
        BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(2),
            duration: TimeSpan.FromHours(1),
            timezone: "Europe/London",
            isRemote: false,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now.AddHours(-1),
            expiresUtc: now.AddHours(2));

    private static BookingSlot CreateSlot(
        string id,
        string transactionId,
        DateTime startUtc,
        DateTime endUtc,
        int travelMinutes,
        int companyBufferMinutes) =>
        BookingSlot.Rehydrate(
            id: id,
            transactionRef: transactionId,
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: startUtc,
            endUtc: endUtc,
            score: 5,
            scoreBreakdown: null,
            locationRef: "loc-1",
            travelMinutes: travelMinutes,
            companyBufferMinutes: companyBufferMinutes,
            distanceMiles: 12,
            travelStatus: "Eligible",
            travelMessage: null,
            createdUtc: startUtc.AddHours(-1));

    private sealed class EventRecorder
    {
        private int _sequence;
        public List<(int Index, string Name)> Events { get; } = [];
        public void Record(string name) => Events.Add((++_sequence, name));
    }

    private sealed class TrackingHoldRepository : IBookingHoldRepository
    {
        private readonly BookingHold? _existingActiveHold;
        private readonly EventRecorder _recorder;

        public TrackingHoldRepository(BookingHold? existingActiveHold, EventRecorder recorder)
        {
            _existingActiveHold = existingActiveHold;
            _recorder = recorder;
        }

        public BookingHold? AddedHold { get; private set; }
        public int ActiveLookupCallCount { get; private set; }

        public Task AddAsync(BookingHold hold, CancellationToken ct)
        {
            AddedHold = hold;
            _recorder.Record("add-new-hold");
            return Task.CompletedTask;
        }

        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_existingActiveHold);
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_existingActiveHold);
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);

        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct)
        {
            if (_existingActiveHold is not null &&
                _existingActiveHold.Status == BookingHoldStatus.Active &&
                _existingActiveHold.SlotId == slotId &&
                _existingActiveHold.ExpiresUtc > utcNow)
            {
                return Task.FromResult<BookingHold?>(_existingActiveHold);
            }

            return Task.FromResult<BookingHold?>(null);
        }

        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct)
        {
            if (_existingActiveHold is not null &&
                _existingActiveHold.Status == BookingHoldStatus.Active &&
                _existingActiveHold.ExpiresUtc > utcNow)
            {
                return Task.FromResult<BookingHold?>(_existingActiveHold);
            }

            return Task.FromResult<BookingHold?>(null);
        }

        public Task<ActiveHoldLookupResult> GetActiveForCreateHoldAsync(string transactionId, string slotId, DateTime utcNow, CancellationToken ct)
        {
            ActiveLookupCallCount++;
            var transactionHold = _existingActiveHold is not null &&
                                  _existingActiveHold.Status == BookingHoldStatus.Active &&
                                  _existingActiveHold.ExpiresUtc > utcNow
                ? _existingActiveHold
                : null;

            var slotHold = transactionHold is not null &&
                           string.Equals(transactionHold.SlotId, slotId, StringComparison.OrdinalIgnoreCase)
                ? transactionHold
                : null;

            return Task.FromResult(new ActiveHoldLookupResult(transactionHold, slotHold));
        }

        public Task UpdateAsync(BookingHold hold, CancellationToken ct)
        {
            if (ReferenceEquals(hold, _existingActiveHold))
                _recorder.Record("update-old-hold");

            return Task.CompletedTask;
        }

        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_existingActiveHold);
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<int> CountActiveOrConfirmedByAdviserAsync(string adviserId, DateTime fromUtc, DateTime toUtc, DateTime utcNow, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class StubSlotRepository : IBookingSlotRepository
    {
        private readonly BookingSlot _slot;
        public StubSlotRepository(BookingSlot slot) => _slot = slot;
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingSlot?>(_slot);
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingSlot>>([_slot]);
        public Task AddAsync(BookingSlot slot, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubTransactionRepository : IBookingTransactionRepository
    {
        private readonly BookingTransaction _transaction;
        public StubTransactionRepository(BookingTransaction transaction) => _transaction = transaction;
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_transaction);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_transaction);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_transaction);
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class StubClientDirectory : IClientDirectory
    {
        public Task<ClientDirectoryItem?> GetAsync(string transactionRef, CancellationToken ct) => Task.FromResult<ClientDirectoryItem?>(null);
    }

    private sealed class StubProfiles : IAdviserProfileProjectionRepository
    {
        private readonly AdviserProfileProjectionRecord? _record;

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
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>(_record is null ? [] : [_record]);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>(_record is null ? [] : [_record]);
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
        {
            ResolveCallCount++;
            return Task.FromResult(_record is not null && string.Equals(_record.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase) ? _record : null);
        }
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class TrackingCalendarGateway : ICalendarGateway
    {
        private readonly EventRecorder _recorder;

        public TrackingCalendarGateway(EventRecorder recorder) => _recorder = recorder;

        public AdviserAvailabilityResult AvailabilityToReturn { get; set; } = new()
        {
            IsFree = true,
            MailboxUnavailable = false,
            StatusMessage = "Free",
            Conflicts = []
        };

        public DateTime LastAvailabilityStartUtc { get; private set; }
        public DateTime LastAvailabilityEndUtc { get; private set; }
        public string? LastAvailabilityUserId { get; private set; }
        public string? LastFreshnessMode { get; private set; }
        public string? CancelledEventId { get; private set; }
        public string? LastCreatedUserId { get; private set; }
        public bool CreatedBookingEvent { get; private set; }
        public string? LastCreatedBody { get; private set; }

        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
        {
            CreatedBookingEvent = true;
            LastCreatedUserId = ev.UserId;
            LastCreatedBody = ev.Body;
            _recorder.Record("create-calendar");
            return Task.FromResult<string?>("evt-new");
        }

        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct)
        {
            CancelledEventId = providerEventId;
            _recorder.Record("cancel-calendar");
            return Task.CompletedTask;
        }

        public Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default) => Task.FromResult<CalendarEventDetails?>(null);

        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(
            string userId,
            DateTime startUtc,
            DateTime endUtc,
            string timezone,
            string? freshnessMode,
            CancellationToken ct)
        {
            LastAvailabilityUserId = userId;
            LastAvailabilityStartUtc = startUtc;
            LastAvailabilityEndUtc = endUtc;
            LastFreshnessMode = freshnessMode;
            _recorder.Record("check-availability");
            return Task.FromResult(AvailabilityToReturn);
        }
    }
}