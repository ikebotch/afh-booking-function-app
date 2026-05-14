using AFH.Booking.Application.Calendar.Handlers;

namespace AFH.Booking.Tests;

public sealed class CalendarViewHandlerTests
{
    [Fact]
    public async Task HandleAsync_UsesMailboxUserIdForCalendarLookup_AndReturnsBusinessAdviserId()
    {
        var calendar = new StubCalendarGateway();
        var sut = new CalendarViewQueryHandler(calendar);

        var result = await sut.HandleAsync(new CalendarViewQuery
        {
            AdviserList =
            [
                new AdviserDirectoryItem
                {
                    AdviserId = "adv-1",
                    Name = "Adviser One",
                    Email = "adviser.one@tenant.com"
                }
            ],
            StartUtc = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 04, 02, 10, 0, 0, DateTimeKind.Utc),
            Timezone = "Europe/London"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("adviser.one@tenant.com", calendar.LastUserId);
        Assert.Equal("ForceRefresh", calendar.LastFreshnessMode);
        Assert.Equal("adv-1", Assert.Single(result.Value!).AdviserId);
    }

    private sealed class StubCalendarGateway : ICalendarGateway
    {
        public string? LastUserId { get; private set; }
        public string? LastFreshnessMode { get; private set; }

        public Task<string?> CreateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> UpdateBookingEventAsync(BookingCalendarEvent ev, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task CancelBookingEventAsync(string userId, string providerEventId, CancellationToken ct) => Task.CompletedTask;
        public Task<CalendarEventDetails?> GetEventAsync(string userId, string eventId, CancellationToken ct = default) => Task.FromResult<CalendarEventDetails?>(null);

        public Task<AdviserAvailabilityResult> CheckAvailabilityAsync(string userId, DateTime startUtc, DateTime endUtc, string timezone, string? freshnessMode, CancellationToken ct)
        {
            LastUserId = userId;
            LastFreshnessMode = freshnessMode;
            return Task.FromResult(new AdviserAvailabilityResult
            {
                IsFree = false,
                MailboxUnavailable = false,
                StatusMessage = "Busy",
                Conflicts =
                [
                    new CalendarConflictBlock
                    {
                        StartUtc = startUtc,
                        EndUtc = endUtc,
                        Subject = "Blocked"
                    }
                ]
            });
        }
    }
}