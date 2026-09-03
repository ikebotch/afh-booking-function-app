using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Availability;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Common;
using AFH.Booking.Domain.Location;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

public class AvailabilityServiceTests
{
    private readonly Mock<IBookingTransactionRepository> _txRepo;
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IClock> _clock;
    private readonly Mock<ITimeZoneProvider> _timeZoneProvider;
    private readonly Mock<IProspectResolver> _prospectResolver;
    private readonly Mock<IAvailabilityTransactionGuard> _transactionGuard;
    private readonly Mock<ISlotStartBuilder> _slotStartBuilder;
    private readonly Mock<IAdviserPoolBuilder> _adviserPoolBuilder;
    private readonly Mock<IAvailabilitySlotProcessor> _slotProcessor;
    private readonly Mock<IAvailabilityResponseBuilder> _responseBuilder;
    private readonly AvailabilityService _sut;

    public AvailabilityServiceTests()
    {
        _txRepo = new Mock<IBookingTransactionRepository>();
        _uow = new Mock<IUnitOfWork>();
        _clock = new Mock<IClock>();
        _timeZoneProvider = new Mock<ITimeZoneProvider>();
        _prospectResolver = new Mock<IProspectResolver>();
        _transactionGuard = new Mock<IAvailabilityTransactionGuard>();
        _slotStartBuilder = new Mock<ISlotStartBuilder>();
        _adviserPoolBuilder = new Mock<IAdviserPoolBuilder>();
        _slotProcessor = new Mock<IAvailabilitySlotProcessor>();
        _responseBuilder = new Mock<IAvailabilityResponseBuilder>();

        _clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 04, 02, 8, 0, 0, DateTimeKind.Utc));

        _sut = new AvailabilityService(
            _txRepo.Object,
            _uow.Object,
            _clock.Object,
            _timeZoneProvider.Object,
            _prospectResolver.Object,
            _transactionGuard.Object,
            _slotStartBuilder.Object,
            _adviserPoolBuilder.Object,
            _slotProcessor.Object,
            _responseBuilder.Object);
    }

    // -------------------------------------------------------------------------
    // Failure paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WhenNeitherClientIdNorTransactionIdProvided_ReturnsBadRequest()
    {
        // Duration > 0 but no ClientId/TransactionId — hits ValidateQuery guard
        var q = new GetAvailabilityQuery { Duration = 60 };

        var result = await _sut.HandleAsync(q, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        // No service calls should have been made
        _prospectResolver.Verify(p => p.ResolveAsync(It.IsAny<GetAvailabilityQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenProspectResolverFails_ReturnsError()
    {
        // Valid query — needs ClientId/TransactionId AND Duration > 0
        var q = new GetAvailabilityQuery { ClientId = "client-1", Duration = 60, PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc) };

        var failedProspect = (
            Value: (ClientDirectoryItem?)null,
            Error: (Result<GetAvailabilityResponse>?)Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.NotFound, "Prospect not found"));

        _prospectResolver.Setup(p => p.ResolveAsync(q, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedProspect);

        var result = await _sut.HandleAsync(q, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        _transactionGuard.Verify(t => t.EnsureOpenAsync(It.IsAny<GetAvailabilityQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTransactionGuardFails_ReturnsConflict()
    {
        var q = new GetAvailabilityQuery { ClientId = "client-1", Duration = 60, PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc) };

        var okProspect = (Value: (ClientDirectoryItem?)new ClientDirectoryItem { TransactionId = "tx-1" }, Error: (Result<GetAvailabilityResponse>?)null);
        _prospectResolver.Setup(p => p.ResolveAsync(q, It.IsAny<CancellationToken>()))
            .ReturnsAsync(okProspect);

        var closedError = Result<GetAvailabilityResponse>.Fail(HttpStatusCode.Conflict, "TransactionClosed", "TransactionClosed");
        _transactionGuard.Setup(t => t.EnsureOpenAsync(q, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedError);

        var result = await _sut.HandleAsync(q, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("TransactionClosed", result.ErrorCode);
        _slotStartBuilder.Verify(s => s.BuildPage(It.IsAny<GetAvailabilityQuery>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSlotStarts_ReturnsEmpty()
    {
        var q = new GetAvailabilityQuery { ClientId = "client-1", Duration = 60, PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc) };

        var okProspect = (Value: (ClientDirectoryItem?)new ClientDirectoryItem(), Error: (Result<GetAvailabilityResponse>?)null);
        _prospectResolver.Setup(p => p.ResolveAsync(q, It.IsAny<CancellationToken>())).ReturnsAsync(okProspect);
        _transactionGuard.Setup(t => t.EnsureOpenAsync(q, It.IsAny<CancellationToken>())).ReturnsAsync((Result<GetAvailabilityResponse>?)null);

        _slotStartBuilder.Setup(s => s.BuildPage(q)).Returns((new List<DateTime>() as IReadOnlyList<DateTime>, "next-cursor"));

        var emptyResponse = Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse());
        _responseBuilder.Setup(r => r.Empty("next-cursor")).Returns(emptyResponse);

        var result = await _sut.HandleAsync(q, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _adviserPoolBuilder.Verify(a => a.BuildAsync(
            It.IsAny<GetAvailabilityQuery>(), It.IsAny<ClientDirectoryItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Success path: orchestration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Success_OrchestratesAllSubservices_AndPersists()
    {
        var q = new GetAvailabilityQuery { ClientId = "client-1", Duration = 60, PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc) };
        var prospect = new ClientDirectoryItem { TransactionId = "tx-1" };

        // TimeZoneProvider must return a valid value so CreateTransaction doesn't throw a domain exception
        _timeZoneProvider.Setup(t => t.DefaultTimeZoneId).Returns("UTC");

        _prospectResolver.Setup(p => p.ResolveAsync(q, It.IsAny<CancellationToken>()))
            .ReturnsAsync((prospect, (Result<GetAvailabilityResponse>?)null));

        _transactionGuard.Setup(t => t.EnsureOpenAsync(q, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Result<GetAvailabilityResponse>?)null);

        var slotStarts = new List<DateTime> { new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc) } as IReadOnlyList<DateTime>;
        var generatedSlotStarts = new List<DateTime>
        {
            new DateTime(2026, 04, 02, 7, 30, 0, DateTimeKind.Utc),
            slotStarts[0]
        };
        _slotStartBuilder.Setup(s => s.BuildPage(q)).Returns((generatedSlotStarts, "next-cursor"));

        var adviser = new AdviserProjectionItem { AdviserId = "adv-1", Name = "Adviser One" };
        var travelMap = new Dictionary<string, LocationCandidate>() as IReadOnlyDictionary<string, LocationCandidate>;
        var poolResult = new AdviserPoolResult(new[] { adviser }, travelMap);
        _adviserPoolBuilder.Setup(a => a.BuildAsync(q, prospect, It.IsAny<CancellationToken>()))
            .ReturnsAsync((poolResult, null));

        var slotResult = new AvailabilitySlotResult(
            "key-1", "adv-1", "Adviser One", false,
            BookingSlot.Rehydrate("slot-1", "tx-1", "adv-1", "Adviser One",
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 5, null, null, 0, 0, null, null, null, DateTime.UtcNow));

        var processedSlots = new[] { slotResult } as IReadOnlyList<AvailabilitySlotResult>;
        _slotProcessor.Setup(s => s.ProcessAsync(
                q,
                It.Is<IReadOnlyList<AdviserProjectionItem>>(l => l.Count == 1 && l[0].AdviserId == "adv-1"),
                slotStarts,
                It.IsAny<BookingTransaction>(),
                It.IsAny<IReadOnlyDictionary<string, LocationCandidate>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedSlots);

        var finalResponse = Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse { TransactionId = "some-tx-id" });
        _responseBuilder.Setup(r => r.Success(
                q,
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AvailabilitySlotResult>>(),
                "next-cursor"))
            .Returns(finalResponse);

        var result = await _sut.HandleAsync(q, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("some-tx-id", result.Value!.TransactionId);

        // Service must persist the new transaction
        _txRepo.Verify(t => t.AddAsync(It.IsAny<BookingTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
