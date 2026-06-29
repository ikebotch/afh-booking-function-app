using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;

namespace AFH.Booking.Tests;

public sealed class BookingLifecycleRecorderTests
{
    [Theory]
    [InlineData(LifecycleEventTypes.Booked, LifecycleStates.Booked)]
    [InlineData(LifecycleEventTypes.Cancelled, LifecycleStates.Cancelled)]
    [InlineData(LifecycleEventTypes.Rearranged, LifecycleStates.Rearranged)]
    [InlineData(LifecycleEventTypes.NoShow, LifecycleStates.NoShow)]
    public async Task RecordEventAsync_UsesActorContextForActorSourceAndCorrelation(
        string eventType,
        string expectedState)
    {
        LifecycleAuditEntry? captured = null;
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LifecycleAuditEntry, CancellationToken>((entry, _) => captured = entry)
            .ReturnsAsync("event-1");
        var recorder = new BookingLifecycleRecorder(audit.Object);
        var actor = BookingActorContext.Partner(
            partnerName: "PartnerCo",
            actorId: "partner-user",
            displayName: "Partner User",
            correlationId: "ctx-corr");

        var eventId = await recorder.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: "booking-1",
            TransactionId: "tx-1",
            EventType: eventType,
            ActorContext: actor,
            ActorType: LifecycleActors.Client,
            ActorId: "legacy-actor",
            ReasonCode: "reason",
            ReasonNotes: "notes",
            Before: new { state = "before" },
            After: new { state = "after" },
            OccurredUtc: new DateTime(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc),
            CorrelationId: "legacy-corr",
            SourceSystem: "BookingService"), CancellationToken.None);

        Assert.Equal("event-1", eventId);
        Assert.NotNull(captured);
        Assert.Equal(eventType, captured!.EventType);
        Assert.Equal(expectedState, captured.NewState);
        Assert.Equal(LifecycleActors.Partner, captured.ActorType);
        Assert.Equal("partner-user", captured.ActorId);
        Assert.Equal("PartnerCo", captured.PartnerName);
        Assert.Equal("ctx-corr", captured.CorrelationId);
        Assert.Equal(BookingActorContext.SourcePartner, captured.SourceSystem);
    }

    [Fact]
    public async Task RecordEventAsync_FallsBackToLegacyCommandFieldsWhenActorContextIsNull()
    {
        LifecycleAuditEntry? captured = null;
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LifecycleAuditEntry, CancellationToken>((entry, _) => captured = entry)
            .ReturnsAsync("event-1");
        var recorder = new BookingLifecycleRecorder(audit.Object);

        await recorder.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: "booking-1",
            TransactionId: "tx-1",
            EventType: LifecycleEventTypes.Cancelled,
            ActorContext: null,
            ActorType: LifecycleActors.Client,
            ActorId: "legacy-actor",
            ReasonCode: "reason",
            ReasonNotes: "notes",
            Before: null,
            After: null,
            OccurredUtc: new DateTime(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc),
            CorrelationId: "legacy-corr",
            SourceSystem: "BookingService",
            PreviousState: LifecycleStates.Booked), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(LifecycleActors.Client, captured!.ActorType);
        Assert.Equal("legacy-actor", captured.ActorId);
        Assert.Equal("legacy-corr", captured.CorrelationId);
        Assert.Equal("BookingService", captured.SourceSystem);
        Assert.Equal(LifecycleStates.Cancelled, captured.NewState);
    }

    [Fact]
    public async Task RecordEventAsync_PreservesNullStateForAuditStyleEvent()
    {
        LifecycleAuditEntry? captured = null;
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LifecycleAuditEntry, CancellationToken>((entry, _) => captured = entry)
            .ReturnsAsync("event-1");
        var recorder = new BookingLifecycleRecorder(audit.Object);
        var actor = BookingActorContext.SelfServiceClient("client-1", "ctx-corr");

        await recorder.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: "hold-1",
            TransactionId: "tx-1",
            EventType: LifecycleEventTypes.HoldCreated,
            ActorContext: actor,
            ActorType: LifecycleActors.System,
            ActorId: null,
            ReasonCode: null,
            ReasonNotes: null,
            Before: null,
            After: new { status = "Held" },
            OccurredUtc: new DateTime(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc),
            CorrelationId: null,
            SourceSystem: "BookingService",
            TriggerReason: "CreateHold"), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(LifecycleEventTypes.HoldCreated, captured!.EventType);
        Assert.Null(captured.NewState);
        Assert.Equal(LifecycleActors.Client, captured.ActorType);
        Assert.Equal("client-1", captured.ActorId);
        Assert.Equal("ctx-corr", captured.CorrelationId);
        Assert.Equal(BookingActorContext.SourceSelfService, captured.SourceSystem);
    }

    [Fact]
    public async Task RecordStepAsync_UsesActorContextCorrelationWhenAvailable()
    {
        LifecycleAuditStepEntry? captured = null;
        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordStepAsync(It.IsAny<LifecycleAuditStepEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LifecycleAuditStepEntry, CancellationToken>((entry, _) => captured = entry)
            .Returns(Task.CompletedTask);
        var recorder = new BookingLifecycleRecorder(audit.Object);
        var actor = BookingActorContext.SystemJob("job-1", "ctx-corr");

        await recorder.RecordStepAsync("event-1", new BookingLifecycleStepRecord(
            LifecycleStepNames.SqlAudit,
            1,
            LifecycleStepStatuses.Succeeded,
            new DateTime(2026, 06, 04, 10, 0, 0, DateTimeKind.Utc),
            CorrelationId: "legacy-corr",
            ActorContext: actor), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("event-1", captured!.LifecycleEventId);
        Assert.Equal(LifecycleStepNames.SqlAudit, captured.StepName);
        Assert.Equal(1, captured.Sequence);
        Assert.Equal(LifecycleStepStatuses.Succeeded, captured.Status);
        Assert.Equal("ctx-corr", captured.CorrelationId);
    }
}
