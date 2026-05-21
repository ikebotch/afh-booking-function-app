using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Holds;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Tests;

public sealed class SelectedSlotRouteTimeGuardTests
{
    [Fact]
    public async Task EvaluateAsync_DoesNotCallRouteTime_ForOnlineBooking()
    {
        var client = new RecordingRouteTimeClient();
        var sut = NewGuard(client);

        var result = await sut.EvaluateAsync(
            SlotWithCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: true),
            "hold-1",
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.False(result.WasTriggered);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_CallsRouteTimeOnce_AndAllowsInPersonSlot_WhenWithinCoverage()
    {
        var client = new RecordingRouteTimeClient
        {
            Result = new LocationRouteTimeResult
            {
                Status = LocationRouteTimeStatus.Succeeded,
                TravelTimeMinutes = 45,
                TravelDistanceMiles = 20
            }
        };
        var sut = NewGuard(client);

        var result = await sut.EvaluateAsync(
            SlotWithCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: false),
            "hold-1",
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.True(result.WasTriggered);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(45, result.TravelTimeMinutes);
        Assert.NotNull(client.LastRequest);
        Assert.Equal(51.5014, client.LastRequest.Source.Latitude, 4);
        Assert.Equal(-0.1419, client.LastRequest.Source.Longitude, 4);
        Assert.Equal(53.4794, client.LastRequest.Destination.Latitude, 4);
        Assert.Equal(-2.2453, client.LastRequest.Destination.Longitude, 4);
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotCallRouteTime_WhenGuardIsDisabled()
    {
        var client = new RecordingRouteTimeClient();
        var sut = NewGuard(client, new FinalRouteTimeGuardOptions { Enabled = false });

        var result = await sut.EvaluateAsync(
            SlotWithCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: false),
            "hold-1",
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.False(result.WasTriggered);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_CanBypassLegacyMissingCoordinates_WhenConfigured()
    {
        var client = new RecordingRouteTimeClient();
        var sut = NewGuard(client, new FinalRouteTimeGuardOptions
        {
            AllowLegacyMissingCoordinates = true
        });

        var result = await sut.EvaluateAsync(
            SlotWithoutCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: false),
            "hold-1",
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.False(result.WasTriggered);
        Assert.Equal(0, client.CallCount);
    }

    [Theory]
    [InlineData(LocationRouteTimeStatus.RouteUnavailable)]
    [InlineData(LocationRouteTimeStatus.Failed)]
    public async Task EvaluateAsync_BlocksGracefully_WhenRouteTimeDoesNotSucceed(LocationRouteTimeStatus status)
    {
        var client = new RecordingRouteTimeClient
        {
            Result = new LocationRouteTimeResult
            {
                Status = status
            }
        };
        var sut = NewGuard(client);

        var result = await sut.EvaluateAsync(
            SlotWithCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: false),
            "hold-1",
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.True(result.WasTriggered);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(Errors.ExactRouteTimeUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_BlocksGracefully_WhenExactRouteExceedsAdviserCoverage()
    {
        var client = new RecordingRouteTimeClient
        {
            Result = new LocationRouteTimeResult
            {
                Status = LocationRouteTimeStatus.Succeeded,
                TravelTimeMinutes = 95,
                TravelDistanceMiles = 20
            }
        };
        var sut = NewGuard(client);

        var result = await sut.EvaluateAsync(
            SlotWithCoordinates(DateTime.UtcNow),
            Transaction(DateTime.UtcNow, isRemote: false),
            "hold-1",
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(Errors.ExactRouteTimeUnavailable, result.ErrorCode);
    }

    private static SelectedSlotRouteTimeGuard NewGuard(
        RecordingRouteTimeClient client,
        FinalRouteTimeGuardOptions? options = null)
        => new(
            client,
            new StubProfiles(),
            Options.Create(options ?? new FinalRouteTimeGuardOptions()),
            NullLogger<SelectedSlotRouteTimeGuard>.Instance);

    private static BookingSlot SlotWithCoordinates(DateTime now)
    {
        var slot = BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 30,
            companyBufferMinutes: 30,
            distanceMiles: 20m,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now);

        slot.AttachTravelSnapshot(
            travelMinutes: 30,
            distanceMiles: 20,
            companyBufferMinutes: 30,
            sourceLocationRef: "adv-1",
            sourcePostcode: "M1 1AE",
            sourceLatitude: 53.4794,
            sourceLongitude: -2.2453,
            destinationLocationRef: "client-1",
            destinationPostcode: "SW1A 1AA",
            destinationLatitude: 51.5014,
            destinationLongitude: -0.1419,
            provider: "LocationService",
            confidence: "High",
            calculatedUtc: now);

        return slot;
    }

    private static BookingSlot SlotWithoutCoordinates(DateTime now)
        => BookingSlot.Rehydrate(
            id: "slot-1",
            transactionRef: "tx-1",
            adviserId: "adv-1",
            adviserName: "Adviser One",
            startUtc: now.AddHours(1),
            endUtc: now.AddHours(2),
            score: 5,
            scoreBreakdown: null,
            locationRef: null,
            travelMinutes: 30,
            companyBufferMinutes: 30,
            distanceMiles: 20m,
            travelStatus: null,
            travelMessage: null,
            createdUtc: now);

    private static BookingTransaction Transaction(DateTime now, bool isRemote)
        => BookingTransaction.Rehydrate(
            id: "tx-1",
            transactionRef: "TRX-1",
            proposedStartUtc: now.AddHours(1),
            duration: TimeSpan.FromHours(1),
            timezone: "UTC",
            isRemote: isRemote,
            meetingType: "Review",
            locationRef: null,
            status: BookingTransactionStatus.Open,
            createdUtc: now,
            expiresUtc: now.AddDays(1));

    private sealed class RecordingRouteTimeClient : ILocationRouteTimeClient
    {
        public int CallCount { get; private set; }
        public LocationRouteTimeRequest? LastRequest { get; private set; }
        public LocationRouteTimeResult Result { get; set; } = new()
        {
            Status = LocationRouteTimeStatus.Succeeded,
            TravelTimeMinutes = 30,
            TravelDistanceMiles = 10
        };

        public Task<LocationRouteTimeResult> CalculateAsync(
            LocationRouteTimeRequest request,
            CancellationToken ct)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubProfiles : IAdviserProfileProjectionRepository
    {
        public Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([]);

        public Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdviserProfileProjectionRecord>>([]);

        public Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
            => Task.FromResult<AdviserProfileProjectionRecord?>(new AdviserProfileProjectionRecord
            {
                AdviserId = adviserId,
                MaxTravelTimeMinutes = 90,
                CoverageRadiusMiles = 60
            });
    }
}
