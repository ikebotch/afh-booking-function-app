using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Calendar;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Bookings.Queries;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Score;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class AvailabilityHandlerTests
{
    [Fact]
    public async Task HandleAsync_RemoteWithoutPreferredAdvisers_UsesActiveProjectedAdvisers()
    {
        var txRepo = new StubTransactionRepository();
        var slotRepo = new StubSlotRepository();
        var calendarView = new StubCalendarViewQueryHandler();

        var sut = new AvailabilityHandler(
            new StubSlotScorer(),
            calendarView,
            new StubTravelMatrixService(),
            new StubClientDirectory(),
            new StubProfiles(
            [
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-1",
                    DisplayName = "Adviser One",
                    MailboxUserId = "adviser.one@tenant.com",
                    IsActive = true
                }
            ]),
            txRepo,
            slotRepo,
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 1
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(txRepo.AddedTransaction);
        Assert.Equal("adviser.one@tenant.com", calendarView.LastMailboxUserId);
    }

    [Fact]
    public async Task HandleAsync_RemoteBlockedMailbox_WhenMailboxDiffersFromAdviserId_ReturnsNoAvailability()
    {
        var sut = new AvailabilityHandler(
            new StubSlotScorer(),
            new StubCalendarViewQueryHandler(isBusy: true),
            new StubTravelMatrixService(),
            new StubClientDirectory(),
            new StubProfiles(
            [
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-1",
                    DisplayName = "Adviser One",
                    MailboxUserId = "adviser.one@tenant.com",
                    IsActive = true
                }
            ]),
            new StubTransactionRepository(),
            new StubSlotRepository(),
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 1
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Advisers);
    }

    private sealed class StubSlotScorer : ISlotScorer
    {
        public SlotScoreResult Score(SlotScoringContext ctx) => new() { Score = 5, Breakdown = new Dictionary<string, int> { ["base"] = 5 } };
    }

    private sealed class StubCalendarViewQueryHandler : ICalendarViewQueryHandler
    {
        private readonly bool _isBusy;

        public StubCalendarViewQueryHandler(bool isBusy = false)
        {
            _isBusy = isBusy;
        }

        public string? LastMailboxUserId { get; private set; }

        public Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct)
        {
            LastMailboxUserId = q.AdviserList.FirstOrDefault()?.Email;
            var items = q.AdviserList.Select(x => new CalendarViewDto
            {
                AdviserId = x.AdviserId,
                IsBusy = _isBusy,
                MailboxUnavailable = false,
                Message = _isBusy ? "Busy" : "Free",
                Conflicts = []
            }).ToList();

            return Task.FromResult(Result<List<CalendarViewDto>>.Ok(items));
        }
    }

    private sealed class StubTravelMatrixService : ITravelMatrixService
    {
        public Task<AFH.Booking.Domain.Location.Travel.TravelMatrixResult> GetAsync(AFH.Booking.Domain.Location.Travel.TravelMatrixRequest request, CancellationToken ct) => Task.FromResult(new AFH.Booking.Domain.Location.Travel.TravelMatrixResult());
    }

    private sealed class StubClientDirectory : IClientDirectory
    {
        public Task<AFH.Booking.Domain.Client.ClientDirectoryItem?> GetAsync(string transactionRef, CancellationToken ct) => Task.FromResult<AFH.Booking.Domain.Client.ClientDirectoryItem?>(null);
    }

    private sealed class StubProfiles : IAdviserProfileProjectionRepository
    {
        private readonly IReadOnlyList<AdviserProfileProjectionRecord> _records;

        public StubProfiles(IReadOnlyList<AdviserProfileProjectionRecord> records)
        {
            _records = records;
        }

        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct) => Task.FromResult(_records);
        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<AdviserProfileProjectionRecord>)_records.Where(x => x.IsActive).ToList());
        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct) => Task.FromResult(_records.FirstOrDefault(x => string.Equals(x.AdviserId, adviserId, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class StubTransactionRepository : IBookingTransactionRepository
    {
        public BookingTransaction? AddedTransaction { get; private set; }
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct)
        {
            AddedTransaction = transaction;
            return Task.CompletedTask;
        }

        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(null);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(null);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(null);
    }

    private sealed class StubSlotRepository : IBookingSlotRepository
    {
        public List<BookingSlot> AddedSlots { get; } = [];
        public Task AddRangeAsync(IEnumerable<BookingSlot> slots, CancellationToken ct)
        {
            AddedSlots.AddRange(slots);
            return Task.CompletedTask;
        }

        public Task<BookingSlot?> GetAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingSlot?>(null);
        public Task<IReadOnlyList<BookingSlot>> ListByTransactionAsync(string transactionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingSlot>>([]);
        public Task AddAsync(BookingSlot slot, CancellationToken ct)
        {
            AddedSlots.Add(slot);
            return Task.CompletedTask;
        }
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class StubClock : IClock
    {
        public StubClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class StubTimeZoneProvider : ITimeZoneProvider
    {
        public string DefaultTimeZoneId => "Europe/London";
    }
}
