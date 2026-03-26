using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Governance;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using AFH.Booking.Domain.Transactions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class CalendarGovernanceServiceTests
{
    [Fact]
    public async Task HandleSnapshotAsync_LogsHygieneIssue_AndEscalatesAtThreshold()
    {
        var now = new DateTime(2026, 03, 25, 9, 0, 0, DateTimeKind.Utc);
        var hold = BookingHold.Rehydrate("booking-1", "slot-1", "user-1", BookingHoldStatus.Confirmed, now.AddHours(-1), now.AddHours(1), now, null, null, null, "evt-1");
        var slot = BookingSlot.Rehydrate("slot-1", "tx-1", "adv@example.com", "Adviser", now.AddHours(2), now.AddHours(3), 5, null, null, 15, 30, null, null, null, now);
        var tx = BookingTransaction.Rehydrate("tx-1", "TRX-1", now.AddHours(2), TimeSpan.FromHours(1), "Europe/London", false, "Review", null, BookingTransactionStatus.Completed, now, null);

        var issues = new List<OperationalIssueRecord>
        {
            new(Guid.NewGuid().ToString("N"), OutlookIssueTypes.Hygiene, OutlookIssueCodes.MissingLocation, "Medium", OperationalIssueStatuses.Open, now.AddHours(-1), "old-1", "tx-1", "TRX-1", "adv@example.com", "evt-old", null, null, 0, null),
            new(Guid.NewGuid().ToString("N"), OutlookIssueTypes.Hygiene, OutlookIssueCodes.MissingLocation, "Medium", OperationalIssueStatuses.Open, now.AddMinutes(-30), "old-2", "tx-1", "TRX-1", "adv@example.com", "evt-old-2", null, null, 0, null)
        };
        var notifications = new List<(string Recipient, string EventType)>();

        var sut = new CalendarGovernanceService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubSnapshotRepository(),
            new InMemoryIssueRepository(issues),
            new InMemoryOperationalNotificationService(notifications),
            new StubUnitOfWork(),
            new StubClock(now),
            Options.Create(new OutlookGovernanceOptions
            {
                EscalationThreshold = 3,
                EscalationWindowHours = 24,
                ManagerRecipients = ["manager@example.com"]
            }));

        await sut.HandleSnapshotAsync(
            "adv@example.com",
            "evt-1",
            new CalendarEventDetails
            {
                CalendarId = "cal-1",
                Subject = "AFH Booking",
                StartUtc = now.AddHours(2).AddMinutes(-45),
                EndUtc = now.AddHours(3).AddMinutes(30),
                HasLocation = false,
                ShowAs = "Busy"
            },
            "corr-1",
            CancellationToken.None);

        Assert.Contains(issues, x => x.Code == OutlookIssueCodes.MissingLocation && x.BookingId == "booking-1");
        Assert.Contains(notifications, x => x.Recipient == "adv@example.com" && x.EventType == OutlookIssueCodes.MissingLocation);
        Assert.Contains(notifications, x => x.Recipient == "manager@example.com" && x.EventType == $"{OutlookIssueCodes.MissingLocation}.Escalated");
    }

    [Fact]
    public async Task HandleDeletedEventAsync_LogsControlledReconciliationIssue()
    {
        var now = new DateTime(2026, 03, 25, 9, 0, 0, DateTimeKind.Utc);
        var hold = BookingHold.Rehydrate("booking-2", "slot-2", "user-2", BookingHoldStatus.Confirmed, now.AddHours(-1), now.AddHours(1), now, null, null, null, "evt-2");
        var slot = BookingSlot.Rehydrate("slot-2", "tx-2", "adv@example.com", "Adviser", now.AddHours(1), now.AddHours(2), 5, null, null, 0, 0, null, null, null, now);
        var tx = BookingTransaction.Rehydrate("tx-2", "TRX-2", now.AddHours(1), TimeSpan.FromHours(1), "UTC", true, "Remote", null, BookingTransactionStatus.Completed, now, null);
        var issues = new List<OperationalIssueRecord>();

        var sut = new CalendarGovernanceService(
            new StubHoldRepository(hold),
            new StubSlotRepository(slot),
            new StubTransactionRepository(tx),
            new StubSnapshotRepository(),
            new InMemoryIssueRepository(issues),
            new InMemoryOperationalNotificationService([]),
            new StubUnitOfWork(),
            new StubClock(now),
            Options.Create(new OutlookGovernanceOptions()));

        await sut.HandleDeletedEventAsync("adv@example.com", "evt-2", "corr-2", CancellationToken.None);

        var issue = Assert.Single(issues);
        Assert.Equal(OutlookIssueCodes.DeletionAttemptDetected, issue.Code);
        Assert.Equal(OperationalIssueStatuses.ReconciliationRequired, issue.Status);
    }

    private sealed class StubHoldRepository : IBookingHoldRepository
    {
        private readonly BookingHold _hold;
        public StubHoldRepository(BookingHold hold) => _hold = hold;
        public Task AddAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetForUpdateAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetBySlotIdAsync(string slotId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetByCalendarEventIdAsync(string providerEventId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold.CalendarProviderEventId == providerEventId ? _hold : null);
        public Task<BookingHold?> GetActiveBySlotIdAsync(string slotId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<BookingHold?> GetActiveByTransactionIdAsync(string transactionId, DateTime utcNow, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task UpdateAsync(BookingHold hold, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingHold?> GetTrackedAsync(string holdId, CancellationToken ct) => Task.FromResult<BookingHold?>(_hold);
        public Task<IReadOnlyList<BookingHold>> GetExpiredActiveAsync(DateTime utcNow, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<BookingHold>>([]);
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
        private readonly BookingTransaction _tx;
        public StubTransactionRepository(BookingTransaction tx) => _tx = tx;
        public Task AddAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_tx);
        public Task<BookingTransaction?> GetWithSlotsAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_tx);
        public Task UpdateAsync(BookingTransaction transaction, CancellationToken ct) => Task.CompletedTask;
        public Task<BookingTransaction?> GetForUpdateAsync(string transactionId, CancellationToken ct) => Task.FromResult<BookingTransaction?>(_tx);
    }

    private sealed class StubSnapshotRepository : ICalendarEventSnapshotRepository
    {
        public Task AddAsync(CalendarEventSnapshot snapshot, CancellationToken ct) => Task.CompletedTask;
        public Task<CalendarEventSnapshot?> GetLatestAsync(string userId, string providerEventId, CancellationToken ct) => Task.FromResult<CalendarEventSnapshot?>(null);
    }

    private sealed class InMemoryIssueRepository : IOperationalIssueRepository
    {
        private readonly List<OperationalIssueRecord> _records;
        public InMemoryIssueRepository(List<OperationalIssueRecord> records) => _records = records;
        public Task AddAsync(OperationalIssueRecord record, CancellationToken ct)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task<OperationalIssueRecord?> GetLatestAsync(string adviserId, string providerEventId, string code, CancellationToken ct)
            => Task.FromResult(_records.LastOrDefault(x => x.AdviserId == adviserId && x.ProviderEventId == providerEventId && x.Code == code));

        public Task<int> CountRecentAsync(string adviserId, string code, DateTime sinceUtc, CancellationToken ct)
            => Task.FromResult(_records.Count(x => x.AdviserId == adviserId && x.Code == code && x.DetectedUtc >= sinceUtc));

        public Task UpdateAsync(OperationalIssueRecord record, CancellationToken ct)
        {
            var index = _records.FindIndex(x => x.Id == record.Id);
            if (index >= 0)
                _records[index] = record;

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryOperationalNotificationService : IOperationalNotificationService
    {
        private readonly List<(string Recipient, string EventType)> _notifications;
        public InMemoryOperationalNotificationService(List<(string Recipient, string EventType)> notifications) => _notifications = notifications;
        public Task NotifyAdviserAsync(string adviserId, string bookingId, string? transactionId, string? transactionRef, string eventType, string message, string? correlationId, CancellationToken ct)
        {
            _notifications.Add((adviserId, eventType));
            return Task.CompletedTask;
        }

        public Task NotifyManagersAsync(IReadOnlyList<string> recipients, string bookingId, string? transactionId, string? transactionRef, string eventType, string message, string? correlationId, CancellationToken ct)
        {
            foreach (var recipient in recipients)
                _notifications.Add((recipient, eventType));
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
}
