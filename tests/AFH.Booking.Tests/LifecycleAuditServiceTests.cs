using System.Text.Json;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Lifecycle;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Tests;

public sealed class LifecycleAuditServiceTests
{
    [Fact]
    public async Task RecordEventAsync_PersistsBeforeAndAfterPayloads()
    {
        var events = new List<LifecycleEventRecord>();
        var steps = new List<LifecycleStepRecord>();
        var service = new LifecycleAuditService(
            new InMemoryLifecycleEventRepository(events),
            new InMemoryLifecycleStepRepository(steps),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var eventId = await service.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: "booking-1",
                TransactionId: "tx-1",
                EventType: LifecycleEventTypes.Cancelled,
                ActorType: "Client",
                ActorId: "client-1",
                ReasonCode: "CLIENT_REQUEST",
                ReasonNotes: "Needs a different day",
                Before: new { status = "Confirmed", slotId = "slot-old" },
                After: new { status = "Cancelled", slotId = "slot-old" },
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: "corr-1",
                PreviousState: LifecycleStates.Booked),
            CancellationToken.None);

        Assert.NotEmpty(eventId);
        var persisted = Assert.Single(events);
        Assert.Equal("booking-1", persisted.BookingId);
        Assert.Contains("\"status\":\"Confirmed\"", persisted.BeforeJson);
        Assert.Contains("\"status\":\"Cancelled\"", persisted.AfterJson);
        Assert.Equal(LifecycleStates.Booked, persisted.PreviousState);
        Assert.Equal(LifecycleStates.Cancelled, persisted.NewState);
        Assert.Equal("corr-1", persisted.CorrelationId);
    }

    [Fact]
    public async Task RecordEventAsync_BlocksInvalidLifecycleTransition()
    {
        var events = new List<LifecycleEventRecord>();
        var service = new LifecycleAuditService(
            new InMemoryLifecycleEventRepository(events),
            new InMemoryLifecycleStepRepository(new List<LifecycleStepRecord>()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: "booking-1",
                TransactionId: "tx-1",
                EventType: LifecycleEventTypes.Booked,
                ActorType: LifecycleActors.System,
                ActorId: "system",
                ReasonCode: null,
                ReasonNotes: null,
                Before: null,
                After: new { status = "Confirmed" },
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: "corr-1",
                PreviousState: LifecycleStates.Cancelled,
                NewState: LifecycleStates.Booked),
            CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task RecordEventAsync_DefaultsMissingActorToSystem_AndPersistsTriggerReason()
    {
        var events = new List<LifecycleEventRecord>();
        var service = new LifecycleAuditService(
            new InMemoryLifecycleEventRepository(events),
            new InMemoryLifecycleStepRepository(new List<LifecycleStepRecord>()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await service.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: "booking-1",
                TransactionId: "tx-1",
                EventType: LifecycleEventTypes.NoShow,
                ActorType: null,
                ActorId: "scheduler",
                ReasonCode: null,
                ReasonNotes: null,
                Before: new { state = LifecycleStates.Booked },
                After: new { state = LifecycleStates.NoShow },
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: "corr-1",
                PreviousState: LifecycleStates.Booked,
                NewState: LifecycleStates.NoShow,
                TriggerReason: "MissedAppointmentSweep"),
            CancellationToken.None);

        var persisted = Assert.Single(events);
        Assert.Equal(LifecycleActors.System, persisted.ActorType);
        Assert.Equal("MissedAppointmentSweep", persisted.TriggerReason);
    }

    [Fact]
    public async Task RecordEventAsync_AllowsAuditStyleEventWithoutLifecycleState()
    {
        var events = new List<LifecycleEventRecord>();
        var service = new LifecycleAuditService(
            new InMemoryLifecycleEventRepository(events),
            new InMemoryLifecycleStepRepository(new List<LifecycleStepRecord>()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await service.RecordEventAsync(
            new LifecycleAuditEntry(
                BookingId: "hold-1",
                TransactionId: "tx-1",
                EventType: LifecycleEventTypes.HoldCreated,
                ActorType: LifecycleActors.Client,
                ActorId: "client-1",
                ReasonCode: null,
                ReasonNotes: null,
                Before: null,
                After: new { status = "Held" },
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: "corr-1",
                SourceSystem: BookingActorContext.SourceSelfService,
                NewState: null,
                TriggerReason: "CreateHold"),
            CancellationToken.None);

        var persisted = Assert.Single(events);
        Assert.Equal(LifecycleEventTypes.HoldCreated, persisted.EventType);
        Assert.Null(persisted.NewState);
        Assert.Equal("CreateHold", persisted.TriggerReason);
    }

    [Fact]
    public async Task RecordStepAsync_PersistsTimelineStep()
    {
        var events = new List<LifecycleEventRecord>();
        var steps = new List<LifecycleStepRecord>();
        var service = new LifecycleAuditService(
            new InMemoryLifecycleEventRepository(events),
            new InMemoryLifecycleStepRepository(steps),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await service.RecordStepAsync(
            new LifecycleAuditStepEntry(
                "evt-1",
                "Notifications",
                3,
                "Failed",
                DateTime.UtcNow,
                DateTime.UtcNow,
                "NotificationFailed",
                "Provider timeout",
                "corr-2"),
            CancellationToken.None);

        var step = Assert.Single(steps);
        Assert.Equal("evt-1", step.LifecycleEventId);
        Assert.Equal(3, step.Sequence);
        Assert.Equal("Failed", step.Status);
        Assert.Equal("NotificationFailed", step.ErrorCode);
    }

    private sealed class InMemoryLifecycleEventRepository : ILifecycleEventRepository
    {
        private readonly List<LifecycleEventRecord> _items;

        public InMemoryLifecycleEventRepository(List<LifecycleEventRecord> items)
        {
            _items = items;
        }

        public Task AddAsync(LifecycleEventRecord record, CancellationToken ct)
        {
            _items.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLifecycleStepRepository : ILifecycleStepRepository
    {
        private readonly List<LifecycleStepRecord> _items;

        public InMemoryLifecycleStepRepository(List<LifecycleStepRecord> items)
        {
            _items = items;
        }

        public Task AddAsync(LifecycleStepRecord record, CancellationToken ct)
        {
            _items.Add(record);
            return Task.CompletedTask;
        }
    }
}
