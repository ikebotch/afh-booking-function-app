using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Availability;
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
using System.Net;

namespace AFH.Booking.Tests;

public sealed class AvailabilityHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenTransactionReferenceIsCompleted_ReturnsClosedConflict()
    {
        var closed = BookingTransaction.Rehydrate(
            id: "tx-closed",
            transactionRef: "lead-1",
            proposedStartUtc: new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            duration: TimeSpan.FromMinutes(60),
            timezone: "Europe/London",
            isRemote: true,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Completed,
            createdUtc: new DateTime(2026, 04, 01, 9, 0, 0, DateTimeKind.Utc),
            expiresUtc: null);
        var txRepo = new StubTransactionRepository(closed);
        var sut = new AvailabilityHandler(
            new StubSlotScorer(),
            new StubCalendarViewQueryHandler(),
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
            new StubSlotRepository(),
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            new StubAvailabilityRulesService(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            TransactionId = "lead-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 1
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("TransactionClosed", result.ErrorCode);
        Assert.Null(txRepo.AddedTransaction);
    }

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
            new StubAvailabilityRulesService(),
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
            new StubAvailabilityRulesService(),
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

    [Fact]
    public async Task HandleAsync_RemoteWithRequiredSkills_FiltersProjectedAdvisers()
    {
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
                    IsActive = true,
                    Skills = ["Investments & Wealth", "Pensions & Retirement"]
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "adviser.two@tenant.com",
                    IsActive = true,
                    Skills = ["Investments & Wealth"]
                }
            ]),
            new StubTransactionRepository(),
            new StubSlotRepository(),
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            new StubAvailabilityRulesService(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 1,
            RequiredSkills = ["Investments & Wealth", "Pensions & Retirement"]
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([1], calendarView.BatchSizes);
        Assert.Equal("adviser.one@tenant.com", calendarView.LastMailboxUserId);
    }

    [Fact]
    public async Task HandleAsync_RemoteWithoutRequiredSkills_KeepsAllActiveProjectedAdvisers()
    {
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
                    IsActive = true,
                    Skills = ["Investments & Wealth"]
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "adviser.two@tenant.com",
                    IsActive = true,
                    Skills = ["Pensions & Retirement"]
                }
            ]),
            new StubTransactionRepository(),
            new StubSlotRepository(),
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            new StubAvailabilityRulesService(),
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
        Assert.Equal([2], calendarView.BatchSizes);
    }

    [Fact]
    public async Task HandleAsync_RemoteWithRequiredSkills_NormalizesWhitespaceAndCase()
    {
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
                    IsActive = true,
                    Skills = ["Investments   & Wealth", "Pensions   & Retirement"]
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "adviser.two@tenant.com",
                    IsActive = true,
                    Skills = ["Protection & Insurance"]
                }
            ]),
            new StubTransactionRepository(),
            new StubSlotRepository(),
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            new StubAvailabilityRulesService(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 1,
            RequiredSkills = ["  investments & wealth ", "PENSIONS & RETIREMENT  "]
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([1], calendarView.BatchSizes);
        Assert.Equal("adviser.one@tenant.com", calendarView.LastMailboxUserId);
    }

    [Fact]
    public async Task HandleAsync_InPerson_ReusesTravelAndBatchesCalendarChecksPerSlot()
    {
        var txRepo = new StubTransactionRepository();
        var slotRepo = new StubSlotRepository();
        var calendarView = new StubCalendarViewQueryHandler();
        var travelMatrix = new StubTravelMatrixService(
        [
            new AFH.Booking.Domain.Location.LocationCandidate
            {
                AdviserId = "adv-1",
                MailboxUserId = "adviser.one@tenant.com",
                TravelMinutes = 15,
                DistanceMiles = 10,
                GoldStar = true
            },
            new AFH.Booking.Domain.Location.LocationCandidate
            {
                AdviserId = "adv-2",
                MailboxUserId = "adviser.two@tenant.com",
                TravelMinutes = 20,
                DistanceMiles = 12,
                GoldStar = false
            }
        ]);

        var sut = new AvailabilityHandler(
            new StubSlotScorer(),
            calendarView,
            travelMatrix,
            new StubClientDirectory(new AFH.Booking.Domain.Client.ClientDirectoryItem
            {
                StreetName1 = "1 High Street",
                Town = "London",
                PostalCode = "SW1A 1AA"
            }),
            new StubProfiles(
            [
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-1",
                    DisplayName = "Adviser One",
                    MailboxUserId = "adviser.one@tenant.com",
                    IsActive = true
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "adviser.two@tenant.com",
                    IsActive = true
                }
            ]),
            txRepo,
            slotRepo,
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            new StubAvailabilityRulesService(),
            NullLogger<AvailabilityHandler>.Instance);

        var result = await sut.HandleAsync(new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = false,
            PreferredStart = new DateTime(2026, 04, 02, 0, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            Limit = 10,
            Take = 3
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(txRepo.AddedTransaction);
        Assert.Equal(1, travelMatrix.CallCount);
        Assert.Equal(3, calendarView.CallCount);
        Assert.Equal([2, 2, 2], calendarView.BatchSizes);
        Assert.Equal(6, slotRepo.AddedSlots.Count);
        Assert.All(slotRepo.AddedSlots, slot => Assert.StartsWith("adv-", slot.AdviserId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_ExcludesAdvisersRejectedByAvailabilityRules_AndStoresRuleAudit()
    {
        var slotRepo = new StubSlotRepository();
        var rules = new StubAvailabilityRulesService(new HashSet<string>(["adv-2"], StringComparer.OrdinalIgnoreCase));

        var sut = new AvailabilityHandler(
            new StubSlotScorer(),
            new StubCalendarViewQueryHandler(),
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
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-2",
                    DisplayName = "Adviser Two",
                    MailboxUserId = "adviser.two@tenant.com",
                    IsActive = true
                }
            ]),
            new StubTransactionRepository(),
            slotRepo,
            new StubUnitOfWork(),
            new StubClock(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc)),
            new StubTimeZoneProvider(),
            rules,
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
        Assert.Single(slotRepo.AddedSlots);
        Assert.Equal("adv-1", slotRepo.AddedSlots[0].AdviserId);
        Assert.Equal(2, rules.EvaluationCount);
        Assert.NotNull(slotRepo.AddedSlots[0].ScoreBreakdown);
        Assert.Equal(1, slotRepo.AddedSlots[0].ScoreBreakdown!["rule.workingPatternAllowed"]);
        Assert.Equal(1, slotRepo.AddedSlots[0].ScoreBreakdown!["rule.capacityAllowed"]);
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
        public int CallCount { get; private set; }
        public List<int> BatchSizes { get; } = [];

        public Task<Result<List<CalendarViewDto>>> HandleAsync(CalendarViewQuery q, CancellationToken ct)
        {
            CallCount++;
            BatchSizes.Add(q.AdviserList.Count);
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
        private readonly IReadOnlyList<AFH.Booking.Domain.Location.LocationCandidate> _candidates;

        public StubTravelMatrixService(IReadOnlyList<AFH.Booking.Domain.Location.LocationCandidate>? candidates = null)
        {
            _candidates = candidates ?? [];
        }

        public int CallCount { get; private set; }

        public Task<AFH.Booking.Domain.Location.Travel.TravelMatrixResult> GetAsync(AFH.Booking.Domain.Location.Travel.TravelMatrixRequest request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new AFH.Booking.Domain.Location.Travel.TravelMatrixResult
            {
                Candidates = _candidates.ToList()
            });
        }
    }

    private sealed class StubClientDirectory : IClientDirectory
    {
        private readonly AFH.Booking.Domain.Client.ClientDirectoryItem? _item;

        public StubClientDirectory(AFH.Booking.Domain.Client.ClientDirectoryItem? item = null)
        {
            _item = item;
        }

        public Task<AFH.Booking.Domain.Client.ClientDirectoryItem?> GetAsync(string transactionRef, CancellationToken ct)
            => Task.FromResult(_item);
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
        private readonly BookingTransaction? _latestByRef;

        public StubTransactionRepository(BookingTransaction? latestByRef = null)
        {
            _latestByRef = latestByRef;
        }

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
        public Task<BookingTransaction?> GetLatestByTransactionRefAsync(string transactionRef, CancellationToken ct) => Task.FromResult(_latestByRef);
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

    private sealed class StubAvailabilityRulesService : IAvailabilityRulesService
    {
        private readonly IReadOnlySet<string> _blockedAdviserIds;

        public StubAvailabilityRulesService(IReadOnlySet<string>? blockedAdviserIds = null)
        {
            _blockedAdviserIds = blockedAdviserIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public int EvaluationCount { get; private set; }

        public Task<AvailabilityRuleEvaluation> EvaluateAsync(
            AdviserDirectoryItem adviser,
            DateTime startUtc,
            DateTime endUtc,
            double durationMinutes,
            DateTime utcNow,
            CancellationToken ct)
        {
            EvaluationCount++;
            var allowed = !_blockedAdviserIds.Contains(adviser.AdviserId);
            return Task.FromResult(new AvailabilityRuleEvaluation(
                allowed,
                allowed,
                allowed,
                true,
                allowed ? null : "Capacity",
                new Dictionary<string, int>
                {
                    ["workingPatternAllowed"] = allowed ? 1 : 0,
                    ["capacityAllowed"] = allowed ? 1 : 0,
                    ["minimumDurationAllowed"] = 1
                }));
        }
    }
}
