using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class CancelBookingHandler : ICancelBookingHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IUnitOfWork _uow;
    private readonly ICalendarGateway _calendar;
    private readonly IClock _clock;
    private readonly ILogger<CancelBookingHandler> _logger;

    public CancelBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IClock clock,
        ILogger<CancelBookingHandler> logger)
    {
        _holds = holds;
        _slots = slots;
        _uow = uow;
        _calendar = calendar;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CancelBookingResponse>> HandleAsync(
        CancelBookingCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.", // (really: holdId)
                Errors.Validation);

        var utcNow = _clock.UtcNow;

        // NOTE: cmd.BookingId is actually HoldId in the new model
        // Prefer: _holds.GetForUpdateAsync(cmd.BookingId, ct)
        var hold = await _holds.GetAsync(cmd.BookingId, ct);
        if (hold is null)
            return Result<CancelBookingResponse>.NotFound($"Hold '{cmd.BookingId}' was not found.");

        // Idempotent cancel
        if (hold.Status == BookingHoldStatus.Cancelled)
        {
            return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
            {
                BookingId = hold.Id,
                Status = hold.Status.ToString(),
                CancelledUtc = hold.CancelledUtc ?? utcNow
            });
        }

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Hold has no slotId linked.",
                Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' linked to hold was not found.",
                Errors.Conflict);

        if (string.IsNullOrWhiteSpace(slot.AdviserId))
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                "Slot has no adviserId.",
                Errors.Conflict);

        _logger.LogInformation(
            "Cancelling hold HoldId={HoldId} SlotId={SlotId} AdviserId={AdviserId} ProviderEventId={ProviderEventId}",
            hold.Id, hold.SlotId, slot.AdviserId, hold.CalendarProviderEventId);

        // Domain change
        hold.Cancel(BuildCancelReason(cmd), utcNow);

        // Cancel calendar event (best-effort)
        if (!string.IsNullOrWhiteSpace(hold.CalendarProviderEventId))
        {
            try
            {
                await _calendar.CancelBookingEventAsync(
                    userId: slot.AdviserId,
                    providerEventId: hold.CalendarProviderEventId!,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cancel calendar event for HoldId={HoldId}. Continuing with cancellation.",
                    hold.Id);
            }
        }

        // If your repo returns detached domain objects, keep UpdateAsync().
        // If it returns tracked entities, this can be removed.
        await _holds.UpdateAsync(hold, ct);

        await _uow.SaveChangesAsync(ct);

        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = hold.Id,
            Status = hold.Status.ToString(),
            CancelledUtc = hold.CancelledUtc ?? utcNow
        });
    }

    private static string BuildCancelReason(CancelBookingCommand cmd)
    {
        var explicitReason = string.IsNullOrWhiteSpace(cmd.Reason) ? null : cmd.Reason.Trim();
        if (!string.IsNullOrWhiteSpace(explicitReason))
            return explicitReason;

        var code = string.IsNullOrWhiteSpace(cmd.ReasonCode) ? "Unspecified" : cmd.ReasonCode.Trim();
        var detail = string.IsNullOrWhiteSpace(cmd.ReasonDetail) ? null : cmd.ReasonDetail.Trim();
        var requestedBy = string.IsNullOrWhiteSpace(cmd.RequestedBy) ? null : cmd.RequestedBy.Trim();

        var parts = new List<string> { $"code={code}" };
        if (!string.IsNullOrWhiteSpace(requestedBy))
            parts.Add($"requestedBy={requestedBy}");
        if (!string.IsNullOrWhiteSpace(detail))
            parts.Add($"detail={detail}");

        return string.Join("; ", parts);
    }
}
