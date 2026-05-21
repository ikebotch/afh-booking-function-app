using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class NoShowBookingService : INoShowBookingService
{
    private static readonly HashSet<string> SupportedActors = new(StringComparer.OrdinalIgnoreCase)
    {
        LifecycleActors.System,
        LifecycleActors.Client,
        LifecycleActors.LeadTech,
        LifecycleActors.Adviser,
        LifecycleActors.Unknown
    };

    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly ILifecycleAuditService _audit;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public NoShowBookingService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        ILifecycleAuditService audit,
        IUnitOfWork uow,
        IClock clock)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<RecordNoShowResponse>> HandleAsync(RecordNoShowCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.BadRequest, "bookingId is required.", Errors.Validation);

        var actor = string.IsNullOrWhiteSpace(cmd.RequestedBy)
            ? LifecycleActors.System
            : cmd.RequestedBy.Trim();

        if (!SupportedActors.Contains(actor))
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.BadRequest, $"Actor path '{actor}' is not supported.", Errors.Validation);

        if (!string.IsNullOrWhiteSpace(cmd.ReasonDetail) && cmd.ReasonDetail.Trim().Length > 1000)
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.BadRequest, "reasonDetail must be 1000 characters or fewer.", Errors.Validation);

        var hold = await _holds.GetAsync(cmd.BookingId.Trim(), ct);
        if (hold is null)
            return Result<RecordNoShowResponse>.NotFound($"Booking '{cmd.BookingId}' was not found.");

        if (hold.Status != BookingHoldStatus.Confirmed)
        {
            return Result<RecordNoShowResponse>.Fail(
                HttpStatusCode.Conflict,
                "No Show can only be recorded for confirmed bookings.",
                Errors.Conflict);
        }

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.Conflict, "Booking has no slotId linked.", Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' linked to booking was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<RecordNoShowResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' linked to slot was not found.", Errors.Conflict);

        var now = _clock.UtcNow;
        var eventId = await _audit.RecordEventAsync(new LifecycleAuditEntry(
            BookingId: hold.Id,
            TransactionId: tx.Id,
            EventType: LifecycleEventTypes.NoShow,
            ActorType: actor,
            ActorId: cmd.ActorId,
            ReasonCode: cmd.ReasonCode,
            ReasonNotes: cmd.ReasonDetail,
            Before: CreateSnapshot(hold, slot, tx, LifecycleStates.Booked),
            After: CreateSnapshot(hold, slot, tx, LifecycleStates.NoShow),
            OccurredUtc: now,
            CorrelationId: cmd.CorrelationId,
            SourceSystem: "BookingService",
            RelatedBookingId: null,
            PreviousState: LifecycleStates.Booked,
            NewState: LifecycleStates.NoShow,
            TriggerReason: string.IsNullOrWhiteSpace(cmd.ReasonCode)
                ? "ConfirmedBookingMarkedNoShow"
                : cmd.ReasonCode.Trim()), ct);

        await _audit.RecordStepAsync(new LifecycleAuditStepEntry(
            eventId,
            LifecycleStepNames.SqlAudit,
            1,
            LifecycleStepStatuses.Succeeded,
            now,
            _clock.UtcNow,
            null,
            null,
            cmd.CorrelationId), ct);

        await _uow.SaveChangesAsync(ct);

        return Result<RecordNoShowResponse>.Ok(new RecordNoShowResponse
        {
            BookingId = hold.Id,
            TransactionId = tx.Id,
            LifecycleEventId = eventId,
            PreviousState = LifecycleStates.Booked,
            NewState = LifecycleStates.NoShow,
            RecordedUtc = now
        });
    }

    private static object CreateSnapshot(BookingHold hold, BookingSlot slot, BookingTransaction tx, string lifecycleState)
    {
        return new
        {
            bookingId = hold.Id,
            lifecycleState,
            holdStatus = hold.Status.ToString(),
            holdConfirmedUtc = hold.ConfirmedUtc,
            slotId = slot.Id,
            slotStartUtc = slot.StartUtc,
            slotEndUtc = slot.EndUtc,
            adviserId = slot.AdviserId,
            transactionId = tx.Id,
            transactionRef = tx.TransactionRef,
            transactionStatus = tx.Status.ToString()
        };
    }
}