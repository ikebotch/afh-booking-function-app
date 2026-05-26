using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Location;
using AFH.Booking.Domain.Location.Travel;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AFH.Booking.Tests;

/// <summary>
/// Tests for AdviserPoolBuilder, focusing on:
/// 1. Correct coordination of the travel coverage client with TimeIndependent mode
/// 2. Scalar travel result propagation into the pool result (Booking is travel consumer, not slot owner)
/// </summary>
public class AdviserPoolBuilderTests
{
    private static AdviserProfileProjectionRecord MakeAdviserProfile(
        string id = "adv-1",
        string homePostcode = "SW1A 1AA") => new()
    {
        AdviserId = id,
        DisplayName = "Adviser One",
        MailboxUserId = $"{id}@tenant.com",
        HomePostcode = homePostcode,
        IsActive = true,
        MaxTravelTimeMinutes = 60,
        CoverageRadiusMiles = 30
    };

    private static LocationTravelCoverageResult MakeSuccessfulCoverage(
        string correlationId = "adv-1",
        int travelMinutes = 25) => new()
    {
        SourcePostcode = "E1 1AA",
        Destinations = new List<LocationTravelCoverageOutcome>
        {
            new()
            {
                CorrelationId = correlationId,
                Postcode = "SW1A 1AA",
                Status = LocationTravelCoverageStatus.Succeeded,
                Route = new LocationTravelRouteOutcome
                {
                    TravelTimeMinutes = travelMinutes,
                    TravelDistanceMiles = 5.2,
                    Confidence = "High"
                },
                Coverage = new LocationCoverageOutcome { IsWithinCoverage = true }
            }
        }
    };

    /// <summary>
    /// Core integration assertion:
    /// Booking calls Location with TimeIndependent mode — scalar travel is the default.
    /// The result is used as a travel-impact input, NOT as a slot generator.
    /// </summary>
    [Fact]
    public async Task BuildAsync_InPerson_SendsTimeIndependent_ToLocationClient()
    {
        var travelClient = new Mock<ILocationTravelCoverageClient>();
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        var sut = new AdviserPoolBuilder(
            travelClient.Object,
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var query = new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = false,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60,
            MeetingType = "Review"
        };

        // Prospect has a valid address — required for the in-person travel call
        var prospect = new ClientDirectoryItem
        {
            PostalCode = "E1 1AA",
            StreetName1 = "1 High Street",
            Town = "London"
        };

        profiles.Setup(p => p.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeAdviserProfile() });

        travelClient.Setup(t => t.EvaluateAsync(
                It.IsAny<LocationTravelCoverageRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessfulCoverage());

        await sut.BuildAsync(query, prospect, CancellationToken.None);

        // KEY ASSERTION: Location is called with TimeIndependent — scalar travel mode
        travelClient.Verify(t => t.EvaluateAsync(
            It.Is<LocationTravelCoverageRequest>(r =>
                r.TimingMode == LocationTravelTimingMode.TimeIndependent &&
                r.SourcePostcode == "E1 1AA" &&
                r.RequestedDepartureTime == new DateTimeOffset(query.PreferredStart) &&
                r.RequestedEndTime == new DateTimeOffset(query.PreferredStart.AddMinutes(query.Duration)) &&
                r.SearchIntervalMinutes == 60 &&
                r.Destinations.Count == 1 &&
                r.Destinations[0].CorrelationId == "adv-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Location travel result (TravelMinutes) is propagated into TravelByAdviserId,
    /// confirming Booking uses it as a scalar input to candidate scoring/filtering.
    /// </summary>
    [Fact]
    public async Task BuildAsync_InPerson_PropagatesTotalTravelMinutes_IntoPoolResult()
    {
        var travelClient = new Mock<ILocationTravelCoverageClient>();
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        var sut = new AdviserPoolBuilder(
            travelClient.Object,
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var query = new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = false,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60
        };

        var prospect = new ClientDirectoryItem
        {
            PostalCode = "E1 1AA",
            StreetName1 = "1 High Street",
            Town = "London"
        };

        profiles.Setup(p => p.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeAdviserProfile(id: "adv-1") });

        travelClient.Setup(t => t.EvaluateAsync(It.IsAny<LocationTravelCoverageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessfulCoverage(correlationId: "adv-1", travelMinutes: 35));

        var (poolResult, error) = await sut.BuildAsync(query, prospect, CancellationToken.None);

        Assert.Null(error);
        // The LocationCandidate for adv-1 carries the scalar TravelMinutes from the Location response
        Assert.True(poolResult.TravelByAdviserId.ContainsKey("adv-1"),
            "Adviser must be represented in the travel dictionary so Booking can use their travel time.");
        var candidate = poolResult.TravelByAdviserId["adv-1"];
        Assert.Equal(35, candidate.TravelMinutes);
    }

    [Fact]
    public async Task BuildAsync_InPerson_CarriesCompanyBuffer_ForOperationalSlotWindows()
    {
        var travelClient = new Mock<ILocationTravelCoverageClient>();
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        var sut = new AdviserPoolBuilder(
            travelClient.Object,
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var query = new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = false,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc),
            Duration = 60
        };

        var prospect = new ClientDirectoryItem
        {
            PostalCode = "E1 1AA",
            StreetName1 = "1 High Street",
            Town = "London"
        };

        profiles.Setup(p => p.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeAdviserProfile(id: "adv-1") });

        travelClient.Setup(t => t.EvaluateAsync(It.IsAny<LocationTravelCoverageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessfulCoverage(correlationId: "adv-1", travelMinutes: 35));

        var (poolResult, error) = await sut.BuildAsync(query, prospect, CancellationToken.None);

        Assert.Null(error);
        var candidate = poolResult.TravelByAdviserId["adv-1"];
        Assert.Equal(30, candidate.CompanyBufferMinutes);
        Assert.Equal(30, candidate.Buffers.CompanyBufferMinutes);
        Assert.Equal(65, candidate.Buffers.PreMeetingBufferMinutes);
        Assert.Equal(30, candidate.Buffers.PostMeetingBufferMinutes);
    }

    /// <summary>
    /// Remote meetings bypass Location entirely — no travel call is made.
    /// </summary>
    [Fact]
    public async Task BuildAsync_Remote_SkipsLocationTravelCall()
    {
        var travelClient = new Mock<ILocationTravelCoverageClient>();
        var profiles = new Mock<IAdviserProfileProjectionRepository>();
        var sut = new AdviserPoolBuilder(
            travelClient.Object,
            profiles.Object,
            NullLogger<AdviserPoolBuilder>.Instance);

        var query = new GetAvailabilityQuery
        {
            ClientId = "client-1",
            IsRemote = true,
            PreferredStart = new DateTime(2026, 04, 02, 9, 0, 0, DateTimeKind.Utc)
        };

        profiles.Setup(p => p.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeAdviserProfile() });

        await sut.BuildAsync(query, null, CancellationToken.None);

        travelClient.Verify(t => t.EvaluateAsync(
            It.IsAny<LocationTravelCoverageRequest>(),
            It.IsAny<CancellationToken>()), Times.Never,
            "Remote meetings do not require a travel coverage call to Location.");
    }
}
