using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class BookingCalendarServiceTests
{
    [Fact]
    public async Task CreateHoldEventAsync_RejectsWhenPersistedFullHoldWindowOverlapsCalendarBlock()
    {
        var startUtc = new DateTime(2026, 05, 21, 10, 0, 0, DateTimeKind.Utc);
        var tx = CreateTransaction(startUtc, isRemote: false);
        var slot = CreateSlot(startUtc, startUtc.AddMinutes(30), travelMinutes: 5, companyBufferMinutes: 30);
        var hold = BookingHold.Create(slot.Id, "adviser.one@tenant.com", TimeSpan.FromMinutes(3), startUtc.AddHours(-1));
        var calendar = new StubCalendarGateway(new AdviserAvailabilityResult
        {
            IsFree = false,
            MailboxUnavailable = false,
            StatusMessage = "Conflict",
            Conflicts =
            [
                new CalendarConflictBlock
                {
                    StartUtc = startUtc.AddMinutes(-35),
                    EndUtc = startUtc.AddMinutes(-30),
                    Subject = "Existing block",
                    ProviderEventId = "evt-existing"
                }
            ]
        });

        var sut = new BookingCalendarService(
            calendar,
            new StubHoldRepository(),
            new StubUnitOfWork(),
            new HoldWindowFactory(),
            new StubClientDirectory(),
            new StubBookingTokenService(),
            TestNotificationOptions());

        var result = await sut.CreateHoldEventAsync(
            new BookingContext(slot, tx, "adviser.one@tenant.com"),
            hold,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.BookingConflictBufferViolation, result.ErrorCode);
        Assert.False(calendar.CreatedEvent);
        Assert.Equal(startUtc.AddMinutes(-35), calendar.LastAvailabilityStartUtc);
        Assert.Equal(startUtc.AddMinutes(60), calendar.LastAvailabilityEndUtc);
    }

    [Fact]
    public async Task CreateHoldEventAsync_IncludesSelfServiceLinksInFinalCalendarBody()
    {
        var startUtc = new DateTime(2026, 05, 21, 10, 0, 0, DateTimeKind.Utc);
        var tx = CreateTransaction(startUtc, isRemote: true);
        var slot = CreateSlot(startUtc, startUtc.AddMinutes(30), travelMinutes: 0, companyBufferMinutes: 0);
        var hold = BookingHold.Create(slot.Id, "adviser.one@tenant.com", TimeSpan.FromMinutes(3), startUtc.AddHours(-1));
        var calendar = new StubCalendarGateway(new AdviserAvailabilityResult
        {
            IsFree = true,
            MailboxUnavailable = false,
            Conflicts = []
        });

        var sut = new BookingCalendarService(
            calendar,
            new StubHoldRepository(),
            new StubUnitOfWork(),
            new HoldWindowFactory(),
            new StubClientDirectory(),
            new StubBookingTokenService("hold-token"),
            TestNotificationOptions());

        var result = await sut.CreateHoldEventAsync(
            new BookingContext(slot, tx, "adviser.one@tenant.com"),
            hold,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("View booking: https://client.example/bookings/", calendar.LastCreatedBody);
        Assert.Contains("/cancel?token=hold-token", calendar.LastCreatedBody);
        Assert.Contains("/reschedule?token=hold-token", calendar.LastCreatedBody);
    }

    private static IOptions<NotificationsOptions> TestNotificationOptions()
        => Options.Create(new NotificationsOptions { ClientPortalBaseUrl = "https://client.example" });

    private static BookingTransaction CreateTransaction(DateTime startUtc, bool isRemote) =>
        BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: startUtc,
            duration: TimeSpan.FromMinutes(30),
            timezone: "Europe/London",
            isRemote: isRemote,
            meetingType: "Review",
            locationRef: "loc-1",
            status: BookingTransactionStatus.Open,
            createdUtc: startUtc.AddHours(-1),
            expiresUtc: startUtc.AddHours(1));

    private static BookingSlot CreateSlot(DateTime startUtc, DateTime endUtc, int travelMinutes, int companyBufferMinutes) =>
        BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
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

    private sealed class StubCalendarGateway : ICalendarGateway
    {
        private readonly AdviserAvailabilityResult _availability;

        public StubCalendarGateway(AdviserAvailabilityResult availability)
        {
            _availability = availability;
        }

        public bool CreatedEvent { get; private set; }
        public DateTime LastAvailabilityStartUtc { get; private set; }
        public DateTime LastAvailabilityEndUtc { get; private set; }
        public string LastCreatedBody { get; private set; } = string.Empty;

        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct)
        {
            CreatedEvent = true;
            LastCreatedBody = ev.Body ?? string.Empty;
            return Task.FromResult<string?>("evt-new");
        }

        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct) => Task.CompletedTask;
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
            LastAvailabilityStartUtc = startUtc;
            LastAvailabilityEndUtc = endUtc;
            return Task.FromResult(_availability);
        }
    }

    private sealed class StubHoldRepository : IBookingHoldRepository
    {
        public Task AddAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<int> CountActiveOrConfirmedByAdviserAsync(string adviserId, DateTime fromUtc, DateTime toUtc, DateTime utcNow, CancellationToken ct) => Task.FromResult(0);
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<IReadOnlyList<BookingHold>> GetAllActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(null);
        public Task<ActiveHoldLookupResult> GetActiveForCreateHoldAsync(string transactionId, string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult(new ActiveHoldLookupResult(null, null));
        public Task UpdateAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class StubClientDirectory : IClientDirectory
    {
        public Task<ClientDirectoryItem?> GetAsync(string transactionIdOrClientId, CancellationToken ct) => Task.FromResult<ClientDirectoryItem?>(null);
    }

    private sealed class StubBookingTokenService(string token = "client-token") : IBookingTokenService
    {
        public Task<Result<string>> GenerateClientAccessTokenAsync(string bookingId, CancellationToken ct)
            => Task.FromResult(Result<string>.Ok(token));
    }
}
