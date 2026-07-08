using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using Moq;
using System.Text.Json;

namespace AFH.Booking.Tests;

public sealed class ApprovalWorkflowServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_UsesAdviserActorContextAndIgnoresSpoofedRequester()
    {
        ApprovalWorkflowRecord? capturedRequest = null;
        ApprovalHistoryRecord? capturedHistory = null;
        var store = CreateStore();
        store.Setup(x => x.AddRequestAsync(
                It.IsAny<ApprovalWorkflowRecord>(),
                It.IsAny<ApprovalHistoryRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApprovalWorkflowRecord, ApprovalHistoryRecord, CancellationToken>((request, history, _) =>
            {
                capturedRequest = request;
                capturedHistory = history;
            })
            .Returns(Task.CompletedTask);

        var sut = CreateSut(store.Object);
        var actor = BookingActorContext.AdviserPortal(
            "adviser-1",
            "Ada Adviser",
            "corr-1",
            ["Bookings.ApprovalRequests.Create"]);

        var response = await sut.CreateAsync(new CreateApprovalWorkflowRequest(
            BookingId: "booking-1",
            ChangeType: "Cancel",
            RequestedBy: "Adviser",
            RequesterId: "spoofed-adviser",
            ReasonCode: "CLIENT_REQUEST",
            ReasonDetail: "Client asked to cancel",
            NewSlotId: null,
            CorrelationId: "legacy-corr",
            ActorContext: actor,
            AdviserNote: "Spoke to client"), CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Adviser", capturedRequest!.RequestedBy);
        Assert.Equal("adviser-1", capturedRequest.RequesterId);
        Assert.Equal("adviser-1", capturedHistory!.ActorId);
        Assert.Equal("adviser-1", response.RequesterId);
        Assert.Equal("Alice Client", response.ClientName);
        Assert.Equal("Adviser One", response.AdviserName);
        Assert.Equal(FixedNow.AddDays(1), response.BookingDateTime);
        Assert.Equal("Review", response.MeetingType);
        var note = Assert.Single(response.Notes);
        Assert.Equal("Adviser", note.ActorType);
        Assert.Equal("adviser-1", note.ActorId);
        Assert.Equal("Ada Adviser", note.DisplayName);
        Assert.Equal("Spoke to client", note.Text);
    }

    [Fact]
    public async Task CreateAsync_PersistsProposedAlternativesForManagerReview()
    {
        ApprovalWorkflowRecord? capturedRequest = null;
        var store = CreateStore();
        store.Setup(x => x.AddRequestAsync(
                It.IsAny<ApprovalWorkflowRecord>(),
                It.IsAny<ApprovalHistoryRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApprovalWorkflowRecord, ApprovalHistoryRecord, CancellationToken>((request, _, _) => capturedRequest = request)
            .Returns(Task.CompletedTask);

        var sut = CreateSut(store.Object);

        var response = await sut.CreateAsync(new CreateApprovalWorkflowRequest(
            BookingId: "booking-1",
            ChangeType: "Rearrange",
            RequestedBy: "Adviser",
            RequesterId: "adviser-1",
            ReasonCode: "CLIENT_REQUEST",
            ReasonDetail: "Client needs a later time",
            NewSlotId: null,
            CorrelationId: "corr-1",
            ActorContext: BookingActorContext.AdviserPortal("adviser-1", "Ada Adviser", "corr-1"),
            AdviserNote: "Client prefers afternoon",
            ProposedAlternativeTimes:
            [
                new ApprovalProposedAlternativeTime
                {
                    SlotId = "slot-1",
                    AdviserId = "adviser-1",
                    StartUtc = FixedNow.AddDays(1),
                    EndUtc = FixedNow.AddDays(1).AddHours(1),
                    Note = "First choice",
                    PreferenceOrder = 1
                },
                new ApprovalProposedAlternativeTime
                {
                    SlotId = "slot-2",
                    AdviserId = "adviser-1",
                    StartUtc = FixedNow.AddDays(2),
                    EndUtc = FixedNow.AddDays(2).AddHours(1),
                    Note = "Second choice",
                    PreferenceOrder = 2
                }
            ]), CancellationToken.None);

        Assert.NotNull(capturedRequest?.RequestedPayloadJson);
        Assert.Contains("slot-1", capturedRequest!.RequestedPayloadJson);
        Assert.Equal(2, response.ProposedAlternativeTimes.Count);
        Assert.Equal("slot-1", response.ProposedAlternativeTimes[0].SlotId);
        Assert.Equal("slot-2", response.ProposedAlternativeTimes[1].SlotId);
    }

    [Fact]
    public async Task CreateAsync_RejectsAdviserRequestForAnotherAdvisersBooking()
    {
        var store = CreateStore();
        var sut = CreateSut(store.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CreateAsync(new CreateApprovalWorkflowRequest(
            BookingId: "booking-1",
            ChangeType: "Cancel",
            RequestedBy: "Adviser",
            RequesterId: "adviser-2",
            ReasonCode: "CLIENT_REQUEST",
            ReasonDetail: "Client asked to cancel",
            NewSlotId: null,
            CorrelationId: "corr-1",
            ActorContext: BookingActorContext.AdviserPortal("adviser-2", "Another Adviser", "corr-1"),
            AdviserNote: "Client asked to cancel"), CancellationToken.None));

        Assert.Contains("own bookings", ex.Message);
        store.Verify(x => x.AddRequestAsync(
            It.IsAny<ApprovalWorkflowRecord>(),
            It.IsAny<ApprovalHistoryRecord>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IApprovalWorkflowStore> CreateStore()
    {
        var store = new Mock<IApprovalWorkflowStore>();
        store.Setup(x => x.LoadBookingAsync("booking-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApprovalBooking());
        return store;
    }

    private static ApprovalWorkflowService CreateSut(IApprovalWorkflowStore store)
    {
        var routing = new Mock<IApprovalRoutingService>();
        routing.Setup(x => x.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalRouteTarget("Role", "booking-approvers", "Booking Approvers"));

        var audit = new Mock<ILifecycleAuditService>();
        audit.Setup(x => x.RecordEventAsync(It.IsAny<LifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("audit-1");

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return new ApprovalWorkflowService(
            store,
            routing.Object,
            Mock.Of<ICancellationOrchestrator>(),
            Mock.Of<IRearrangementOrchestrator>(),
            audit.Object,
            Mock.Of<IApprovalNotificationService>(),
            uow.Object,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static ApprovalBookingSnapshot ApprovalBooking()
    {
        var hold = BookingHold.Rehydrate(
            "booking-1",
            "slot-1",
            "user-1",
            BookingHoldStatus.Confirmed,
            FixedNow.AddHours(-2),
            FixedNow.AddHours(1),
            FixedNow.AddHours(-1),
            null,
            null,
            null,
            null,
            null);
        var slot = BookingSlot.Rehydrate(
            "slot-1",
            "tx-1",
            "adviser-1",
            "Adviser One",
            FixedNow.AddDays(1),
            FixedNow.AddDays(1).AddHours(1),
            5,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            FixedNow);
        var tx = BookingTransaction.Rehydrate(
            "tx-1",
            "transaction-ref-1",
            "BK-REF-1",
            "Alice Client",
            "alice@example.com",
            "1 Client Street",
            null,
            "Client Town",
            null,
            "AB1 2CD",
            FixedNow,
            TimeSpan.FromHours(1),
            "Europe/London",
            false,
            "Review",
            null,
            BookingTransactionStatus.Open,
            FixedNow,
            null,
            [slot]);

        return new ApprovalBookingSnapshot(hold, slot, tx);
    }
}
