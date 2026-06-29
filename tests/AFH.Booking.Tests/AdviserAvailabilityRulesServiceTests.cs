using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Bookings.Score;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace AFH.Booking.Tests;

public sealed class AdviserAvailabilityRulesServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 06, 15, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task US5299_AvailabilityResponse_DisplaysAvailableAdviserWithRuleAudit()
    {
        var processor = CreateProcessor(
            rules: new AvailabilityRuleEvaluation(
                true,
                true,
                true,
                true,
                null,
                new Dictionary<string, int>
                {
                    ["workingPatternAllowed"] = 1,
                    ["capacityAllowed"] = 1,
                    ["minimumDurationAllowed"] = 1
                }));

        var transaction = BookingTransaction.Rehydrate(
            "tx-1",
            "client-1",
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(60),
            "UTC",
            true,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            FixedNow.AddMinutes(10));

        var slots = await processor.ProcessAsync(
            new GetAvailabilityQuery
            {
                ClientId = "client-1",
                PreferredStart = new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
                Duration = 60,
                IsRemote = true
            },
            [new AdviserProjectionItem { AdviserId = "adv-1", Name = "Ada Adviser" }],
            [new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc)],
            transaction,
            new Dictionary<string, LocationCandidate>(),
            FixedNow,
            CancellationToken.None);

        var slot = Assert.Single(slots).Slot;
        Assert.Equal("adv-1", slot.AdviserId);
        Assert.NotNull(slot.ScoreBreakdown);
        Assert.Equal(1, slot.ScoreBreakdown!["rule.workingPatternAllowed"]);
        Assert.Equal(1, slot.ScoreBreakdown["rule.capacityAllowed"]);
        Assert.Equal(1, slot.ScoreBreakdown["rule.minimumDurationAllowed"]);
    }

    [Fact]
    public async Task US5303_InPersonAvailability_ExcludesSlotsOutsideWorkingPattern()
    {
        var processor = CreateProcessor(
            rules: new AvailabilityRuleEvaluation(
                false,
                false,
                true,
                true,
                "WorkingPattern",
                new Dictionary<string, int>
                {
                    ["workingPatternAllowed"] = 0,
                    ["capacityAllowed"] = 1,
                    ["minimumDurationAllowed"] = 1
                }));

        var transaction = BookingTransaction.Rehydrate(
            "tx-1",
            "client-1",
            new DateTime(2026, 06, 15, 7, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(60),
            "UTC",
            false,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            FixedNow.AddMinutes(10));

        var slots = await processor.ProcessAsync(
            new GetAvailabilityQuery
            {
                ClientId = "client-1",
                PreferredStart = new DateTime(2026, 06, 15, 7, 0, 0, DateTimeKind.Utc),
                Duration = 60,
                IsRemote = false
            },
            [new AdviserProjectionItem { AdviserId = "adv-1", Name = "Ada Adviser" }],
            [new DateTime(2026, 06, 15, 7, 0, 0, DateTimeKind.Utc)],
            transaction,
            new Dictionary<string, LocationCandidate>
            {
                ["adv-1"] = new() { AdviserId = "adv-1", AdviserName = "Ada Adviser", IsEligible = true }
            },
            FixedNow,
            CancellationToken.None);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task US5304_AvailabilityRules_EnforceMinimumDuration()
    {
        var service = CreateRulesService(new AvailabilityRulesOptions
        {
            MinimumAppointmentMinutes = 45,
            DefaultWorkingDayStart = "08:00",
            DefaultWorkingDayEnd = "17:00"
        });

        var result = await service.EvaluateAsync(
            new AdviserProjectionItem { AdviserId = "adv-1" },
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 15, 10, 30, 0, DateTimeKind.Utc),
            30,
            FixedNow,
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.MinimumDurationAllowed);
        Assert.Equal("MinimumDuration", result.RejectionReason);
    }

    [Fact]
    public async Task US5305_RemoteAvailability_UsesOnlyActiveSkilledProfiles()
    {
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        profiles.Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-skilled",
                    DisplayName = "Skilled Adviser",
                    MailboxUserId = "skilled@example.test",
                    IsActive = true,
                    Skills = ["Pensions"]
                },
                new AdviserProfileProjectionRecord
                {
                    AdviserId = "adv-unskilled",
                    DisplayName = "Unskilled Adviser",
                    MailboxUserId = "unskilled@example.test",
                    IsActive = true,
                    Skills = ["Mortgages"]
                }
            ]);

        var builder = new AdviserPoolBuilder(
            Mock.Of<ILocationTravelCoverageClient>(),
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var (pool, error) = await builder.BuildAsync(
            new GetAvailabilityQuery
            {
                ClientId = "client-1",
                IsRemote = true,
                RequiredSkills = ["Pensions"]
            },
            null,
            CancellationToken.None);

        Assert.Null(error);
        var adviser = Assert.Single(pool.Advisers);
        Assert.Equal("adv-skilled", adviser.AdviserId);
    }

    [Fact]
    public async Task US5306_PreferredAdviserSelection_DoesNotCreateSyntheticIneligibleProfile()
    {
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        profiles.Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AdviserProfileProjectionRecord>());

        var builder = new AdviserPoolBuilder(
            Mock.Of<ILocationTravelCoverageClient>(),
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var (pool, error) = await builder.BuildAsync(
            new GetAvailabilityQuery
            {
                ClientId = "client-1",
                IsRemote = true,
                PreferredAdviserIds = ["missing-adviser"]
            },
            null,
            CancellationToken.None);

        Assert.Null(error);
        Assert.Empty(pool.Advisers);
    }

    [Fact]
    public async Task US5307_AvailabilityRules_EnforceCapacityLimits()
    {
        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.CountActiveOrConfirmedByAdviserAsync(
                "adv-1",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                FixedNow,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateRulesService(new AvailabilityRulesOptions
        {
            CapacityWindowDays = 1,
            CapacityLimits = [new AdviserCapacityOptions { AdviserId = "adv-1", MaxActiveBookings = 1 }]
        }, holds.Object);

        var result = await service.EvaluateAsync(
            new AdviserProjectionItem { AdviserId = "adv-1" },
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 15, 11, 0, 0, DateTimeKind.Utc),
            60,
            FixedNow,
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.CapacityAllowed);
        Assert.Equal("Capacity", result.RejectionReason);
    }

    [Fact]
    public async Task US5307_AvailabilityRules_EnforceDailyWeeklyAndMonthlyCapacityLimits()
    {
        var slotStart = new DateTime(2026, 06, 17, 10, 0, 0, DateTimeKind.Utc);
        var dayStart = new DateTime(2026, 06, 17, 0, 0, 0, DateTimeKind.Utc);
        var weekStart = new DateTime(2026, 06, 15, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc);

        var holds = new Mock<IBookingHoldRepository>();
        holds.Setup(x => x.CountActiveOrConfirmedByAdviserAsync(
                "adv-1",
                dayStart,
                dayStart.AddDays(1),
                FixedNow,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        holds.Setup(x => x.CountActiveOrConfirmedByAdviserAsync(
                "adv-1",
                weekStart,
                weekStart.AddDays(7),
                FixedNow,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        holds.Setup(x => x.CountActiveOrConfirmedByAdviserAsync(
                "adv-1",
                monthStart,
                monthStart.AddMonths(1),
                FixedNow,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        var service = CreateRulesService(new AvailabilityRulesOptions
        {
            CapacityLimits =
            [
                new AdviserCapacityOptions
                {
                    AdviserId = "adv-1",
                    DailyLimit = 3,
                    WeeklyLimit = 4,
                    MonthlyLimit = 12
                }
            ]
        }, holds.Object);

        var result = await service.EvaluateAsync(
            new AdviserProjectionItem { AdviserId = "adv-1" },
            slotStart,
            slotStart.AddHours(1),
            60,
            FixedNow,
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.False(result.CapacityAllowed);
        Assert.Equal("Capacity", result.RejectionReason);

        holds.Verify(x => x.CountActiveOrConfirmedByAdviserAsync(
            "adv-1",
            dayStart,
            dayStart.AddDays(1),
            FixedNow,
            It.IsAny<CancellationToken>()), Times.Once);
        holds.Verify(x => x.CountActiveOrConfirmedByAdviserAsync(
            "adv-1",
            weekStart,
            weekStart.AddDays(7),
            FixedNow,
            It.IsAny<CancellationToken>()), Times.Once);
        holds.Verify(x => x.CountActiveOrConfirmedByAdviserAsync(
            "adv-1",
            monthStart,
            monthStart.AddMonths(1),
            FixedNow,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AvailabilityRulesService_UsesPersistedRulesBeforeFallbackConfig()
    {
        var persisted = new AvailabilityRulesOptions
        {
            MinimumAppointmentMinutes = 45,
            DefaultWorkingDayStart = "09:00",
            DefaultWorkingDayEnd = "17:00"
        };

        var fallback = new AvailabilityRulesOptions
        {
            MinimumAppointmentMinutes = 15,
            DefaultWorkingDayStart = "08:00",
            DefaultWorkingDayEnd = "18:00"
        };

        var service = CreateRulesService(fallback, rules: new StubAvailabilityRulesRepository(persisted));

        var result = await service.EvaluateAsync(
            new AdviserProjectionItem { AdviserId = "adv-1" },
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 15, 10, 30, 0, DateTimeKind.Utc),
            30,
            FixedNow,
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("MinimumDuration", result.RejectionReason);
    }

    [Fact]
    public async Task US5308_CreateHold_RevalidatesSelectedSlotAndRejectsOverCapacityAdviser()
    {
        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adv-1",
            "Ada Adviser",
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 06, 15, 11, 0, 0, DateTimeKind.Utc),
            5,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            FixedNow);
        var transaction = BookingTransaction.Rehydrate(
            "tx-1",
            "client-1",
            slot.StartUtc,
            TimeSpan.FromMinutes(60),
            "UTC",
            true,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            FixedNow.AddMinutes(10));

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.GetAsync("slot-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        var transactions = new Mock<IBookingTransactionRepository>();
        transactions.Setup(x => x.GetAsync("tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        profiles.Setup(x => x.GetAsync("adv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdviserProfileProjectionRecord
            {
                AdviserId = "adv-1",
                DisplayName = "Ada Adviser",
                MailboxUserId = "adv-1@example.test",
                IsActive = true
            });
        var rules = new Mock<IAvailabilityRulesService>();
        rules.Setup(x => x.EvaluateAsync(
                It.IsAny<AdviserProjectionItem>(),
                slot.StartUtc,
                slot.EndUtc,
                60,
                FixedNow,
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AvailabilityRuleEvaluation(
                false,
                true,
                false,
                true,
                "Capacity",
                new Dictionary<string, int> { ["capacityAllowed"] = 0 }));
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(FixedNow);

        var loader = new BookingContextLoader(
            slots.Object,
            transactions.Object,
            profiles.Object,
            rules.Object,
            clock.Object);

        var result = await loader.LoadAsync(
            new CreateHoldCommand { SlotId = "slot-1", TransactionRef = "client-1" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal(Errors.SlotNoLongerAvailable, result.ErrorCode);
        Assert.Contains("Capacity", result.ErrorMessage);
    }

    [Fact]
    public async Task US5316_AvailabilitySlots_PersistRuleDecisionAudit()
    {
        var processor = CreateProcessor(
            rules: new AvailabilityRuleEvaluation(
                true,
                true,
                true,
                true,
                null,
                new Dictionary<string, int>
                {
                    ["workingPatternAllowed"] = 1,
                    ["capacityAllowed"] = 1,
                    ["minimumDurationAllowed"] = 1
                }));
        var transaction = BookingTransaction.Rehydrate(
            "tx-audit",
            "client-1",
            new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(60),
            "UTC",
            false,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            FixedNow.AddMinutes(10));

        var slots = await processor.ProcessAsync(
            new GetAvailabilityQuery
            {
                ClientId = "client-1",
                PreferredStart = new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc),
                Duration = 60,
                IsRemote = false
            },
            [new AdviserProjectionItem { AdviserId = "adv-1", Name = "Ada Adviser" }],
            [new DateTime(2026, 06, 15, 10, 0, 0, DateTimeKind.Utc)],
            transaction,
            new Dictionary<string, LocationCandidate>
            {
                ["adv-1"] = new() { AdviserId = "adv-1", AdviserName = "Ada Adviser", IsEligible = true }
            },
            FixedNow,
            CancellationToken.None);

        var breakdown = Assert.Single(slots).Slot.ScoreBreakdown;
        Assert.NotNull(breakdown);
        Assert.Equal(1, breakdown!["rule.workingPatternAllowed"]);
        Assert.Equal(1, breakdown["rule.capacityAllowed"]);
        Assert.Equal(1, breakdown["rule.minimumDurationAllowed"]);
    }

    private static AvailabilityRulesService CreateRulesService(
        AvailabilityRulesOptions options,
        IBookingHoldRepository? holds = null,
        IAvailabilityRulesRepository? rules = null)
        => new(
            holds ?? Mock.Of<IBookingHoldRepository>(),
            rules ?? new StubAvailabilityRulesRepository(null),
            Options.Create(options));

    private static AvailabilitySlotProcessor CreateProcessor(AvailabilityRuleEvaluation rules)
    {
        var scorer = new Mock<ISlotScorer>();
        scorer.Setup(x => x.Score(It.IsAny<SlotScoringContext>()))
            .Returns(new SlotScoreResult
            {
                Score = 5,
                Breakdown = new Dictionary<string, int> { ["base"] = 5 }
            });

        var calendar = new Mock<ICalendarViewQueryService>();
        calendar.Setup(x => x.HandleAsync(It.IsAny<CalendarViewQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<CalendarViewDto>>.Ok([
                new CalendarViewDto
                {
                    AdviserId = "adv-1",
                    StartUtc = new DateTime(2026, 06, 15, 0, 0, 0, DateTimeKind.Utc),
                    EndUtc = new DateTime(2026, 06, 16, 0, 0, 0, DateTimeKind.Utc),
                    IsBusy = false
                }
            ]));

        var slots = new Mock<IBookingSlotRepository>();
        slots.Setup(x => x.AddAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var timeZone = new Mock<ITimeZoneProvider>();
        timeZone.SetupGet(x => x.DefaultTimeZoneId).Returns("UTC");

        var availabilityRules = new Mock<IAvailabilityRulesService>();
        availabilityRules.Setup(x => x.EvaluateAsync(
                It.IsAny<AdviserProjectionItem>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<double>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()))
            .ReturnsAsync(rules);

        return new AvailabilitySlotProcessor(
            scorer.Object,
            calendar.Object,
            slots.Object,
            timeZone.Object,
            availabilityRules.Object,
            NullLogger<AvailabilitySlotProcessor>.Instance);
    }

    private sealed class StubAvailabilityRulesRepository : IAvailabilityRulesRepository
    {
        private readonly AvailabilityRulesOptions? _rules;

        public StubAvailabilityRulesRepository(AvailabilityRulesOptions? rules)
        {
            _rules = rules;
        }

        public Task<AvailabilityRulesOptions?> GetActiveRulesAsync(CancellationToken ct, string projectContext = "Booking")
            => Task.FromResult(_rules);
    }
}
