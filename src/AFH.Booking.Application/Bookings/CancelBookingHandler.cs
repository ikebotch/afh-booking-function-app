using System.Text.Json;
using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class CancelBookingHandler : ICancelBookingHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IUnitOfWork _uow;
    private readonly ICalendarGateway _calendar;
    private readonly IClock _clock;
    private readonly IClientNotificationService _notifications;
    private readonly IDownstreamUpdateService _downstreamUpdates;
    private readonly ILogger<CancelBookingHandler> _logger;

    public CancelBookingHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IClock clock,
        IClientNotificationService notifications,
        IDownstreamUpdateService downstreamUpdates,
        ILogger<CancelBookingHandler> logger)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _uow = uow;
        _calendar = calendar;
        _clock = clock;
        _notifications = notifications;
        _downstreamUpdates = downstreamUpdates;
        _logger = logger;
    }

    public async Task<Result<CancelBookingResponse>> HandleAsync(
        CancelBookingCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);

        var utcNow = _clock.UtcNow;

        var hold = await _holds.GetAsync(cmd.BookingId, ct);
        if (hold is null)
            return Result<CancelBookingResponse>.NotFound($"Hold '{cmd.BookingId}' was not found.");

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

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<CancelBookingResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' linked to slot was not found.",
                Errors.Conflict);

        _logger.LogInformation(
            "Cancelling hold HoldId={HoldId} SlotId={SlotId} AdviserId={AdviserId} ProviderEventId={ProviderEventId}",
            hold.Id, hold.SlotId, slot.AdviserId, hold.CalendarProviderEventId);

        hold.Cancel(cmd.Reason, utcNow);

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

        await _holds.UpdateAsync(hold, ct);
        await _uow.SaveChangesAsync(ct);

        var notificationMessage = $"Your meeting on {slot.StartUtc:yyyy-MM-dd HH:mm} has been cancelled.";
        await _notifications.SendBookingNotificationAsync(
            bookingId: hold.Id,
            eventType: "BookingCancelled",
            message: notificationMessage,
            sendSms: true,
            sendEmail: true,
            ct: ct);

        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: hold.Id,
            changeType: "Cancel",
            transactionRef: tx.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                bookingId = hold.Id,
                slotId = slot.Id,
                adviserId = slot.AdviserId,
                cancelledUtc = hold.CancelledUtc,
                reason = cmd.Reason
            }),
            ct: ct);

        return Result<CancelBookingResponse>.Ok(new CancelBookingResponse
        {
            BookingId = hold.Id,
            Status = hold.Status.ToString(),
            CancelledUtc = hold.CancelledUtc ?? utcNow
        });
    }
}
