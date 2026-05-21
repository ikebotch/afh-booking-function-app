using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Location.Travel;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Holds;

public sealed class SelectedSlotRouteTimeGuard : ISelectedSlotRouteTimeGuard
{
    private readonly ILocationRouteTimeClient _routeTimeClient;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly FinalRouteTimeGuardOptions _options;
    private readonly ILogger<SelectedSlotRouteTimeGuard> _logger;

    public SelectedSlotRouteTimeGuard(
        ILocationRouteTimeClient routeTimeClient,
        IAdviserProfileProjectionRepository profiles,
        IOptions<FinalRouteTimeGuardOptions> options,
        ILogger<SelectedSlotRouteTimeGuard> logger)
    {
        _routeTimeClient = routeTimeClient;
        _profiles = profiles;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SelectedSlotRouteTimeGuardResult> EvaluateAsync(
        BookingSlot slot,
        BookingTransaction transaction,
        string holdId,
        CancellationToken ct)
    {
        var bookingMode = transaction.IsRemote ? "Online" : "InPerson";
        var correlationId = transaction.Id;
        var coordinatesPresent = HasExactCoordinates(slot);

        if (!_options.Enabled)
        {
            LogDecision(
                bookingMode,
                false,
                holdId,
                correlationId,
                slot.StartUtc,
                coordinatesPresent,
                null,
                null,
                null,
                true);

            return new SelectedSlotRouteTimeGuardResult(true, false, null, null, null, null);
        }

        if (transaction.IsRemote)
        {
            LogDecision(
                bookingMode,
                false,
                holdId,
                correlationId,
                slot.StartUtc,
                coordinatesPresent,
                null,
                null,
                null,
                true);

            return new SelectedSlotRouteTimeGuardResult(true, false, null, null, null, null);
        }

        if (!coordinatesPresent)
        {
            var allowLegacyBypass = _options.AllowLegacyMissingCoordinates;
            LogDecision(
                bookingMode,
                !allowLegacyBypass,
                holdId,
                correlationId,
                slot.StartUtc,
                false,
                LocationRouteTimeStatus.Failed,
                null,
                null,
                allowLegacyBypass);

            if (allowLegacyBypass)
                return new SelectedSlotRouteTimeGuardResult(true, false, null, null, null, null);

            return Block("Exact travel coordinates are unavailable for the selected in-person slot.");
        }

        try
        {
            var result = await _routeTimeClient.CalculateAsync(
                new LocationRouteTimeRequest
                {
                    CorrelationId = correlationId,
                    DepartAt = new DateTimeOffset(DateTime.SpecifyKind(slot.StartUtc, DateTimeKind.Utc)),
                    // The persisted travel snapshot names come from the search-time
                    // enrichment model. For the final exact route-time contract,
                    // source is the client/appointment location and destination is
                    // the adviser/office location.
                    Source = new LocationTravelCoordinates
                    {
                        Latitude = slot.DestinationLatitude!.Value,
                        Longitude = slot.DestinationLongitude!.Value
                    },
                    Destination = new LocationTravelCoordinates
                    {
                        Latitude = slot.SourceLatitude!.Value,
                        Longitude = slot.SourceLongitude!.Value
                    }
                },
                ct);

            if (result.Status != LocationRouteTimeStatus.Succeeded ||
                result.TravelTimeMinutes is null ||
                result.TravelDistanceMiles is null)
            {
                LogDecision(
                    bookingMode,
                    true,
                    holdId,
                    correlationId,
                    slot.StartUtc,
                    true,
                    result.Status,
                    result.TravelTimeMinutes,
                    result.TravelDistanceMiles,
                    false);

                return Block("The selected in-person slot is no longer available because exact travel time could not be confirmed.");
            }

            var adviser = await _profiles.GetAsync(slot.AdviserId, ct);
            var exceedsTime = adviser?.MaxTravelTimeMinutes is { } maxTravelTime
                && result.TravelTimeMinutes.Value > maxTravelTime;
            var exceedsDistance = adviser?.CoverageRadiusMiles is { } maxDistance
                && result.TravelDistanceMiles.Value > maxDistance;
            var allowed = !exceedsTime && !exceedsDistance;

            LogDecision(
                bookingMode,
                true,
                holdId,
                correlationId,
                slot.StartUtc,
                true,
                result.Status,
                result.TravelTimeMinutes,
                result.TravelDistanceMiles,
                allowed);

            if (!allowed)
            {
                return Block("The selected in-person slot is no longer available because exact travel falls outside adviser coverage.");
            }

            return new SelectedSlotRouteTimeGuardResult(
                true,
                true,
                null,
                null,
                result.TravelTimeMinutes,
                result.TravelDistanceMiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Booking final route-time check failed. BookingMode={BookingMode} FinalRouteTimeTriggered={FinalRouteTimeTriggered} CorrelationId={CorrelationId} DepartAt={DepartAt}",
                bookingMode,
                true,
                correlationId,
                slot.StartUtc);

            return Block("The selected in-person slot is no longer available because exact travel time could not be confirmed.");
        }
    }

    private static bool HasExactCoordinates(BookingSlot slot)
        => slot.SourceLatitude.HasValue
           && slot.SourceLongitude.HasValue
           && slot.DestinationLatitude.HasValue
           && slot.DestinationLongitude.HasValue;

    private static SelectedSlotRouteTimeGuardResult Block(string message)
        => new(false, true, message, Errors.ExactRouteTimeUnavailable, null, null);

    private void LogDecision(
        string bookingMode,
        bool finalRouteTimeTriggered,
        string holdId,
        string correlationId,
        DateTime departAt,
        bool coordinatesPresent,
        LocationRouteTimeStatus? status,
        int? travelTimeMinutes,
        double? travelDistanceMiles,
        bool bookingAllowed)
    {
        _logger.LogInformation(
            "Booking final route-time decision. BookingMode={BookingMode} FinalRouteTimeTriggered={FinalRouteTimeTriggered} BookingId={BookingId} HoldId={HoldId} CorrelationId={CorrelationId} DepartAt={DepartAt} CoordinatesPresent={CoordinatesPresent} RouteTimeStatus={RouteTimeStatus} TravelTimeMinutes={TravelTimeMinutes} TravelDistanceMiles={TravelDistanceMiles} BookingAllowed={BookingAllowed}",
            bookingMode,
            finalRouteTimeTriggered,
            holdId,
            holdId,
            correlationId,
            departAt,
            coordinatesPresent,
            status?.ToString(),
            travelTimeMinutes,
            travelDistanceMiles,
            bookingAllowed);
    }
}
