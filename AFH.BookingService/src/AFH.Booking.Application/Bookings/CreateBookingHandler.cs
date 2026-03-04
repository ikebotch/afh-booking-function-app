using AFH.Booking.Application.Abstractions.Bookings.Handlers;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Common;
using AFH.Booking.Domain.Transactions;
using AFH.Booking.Domain.ValueObjects;
using System.Globalization;
using System.Net;

namespace AFH.Booking.Application.Bookings;

public sealed class CreateBookingHandler : ICreateBookingHandler
{
    private static readonly TimeSpan DefaultHoldWindow = TimeSpan.FromMinutes(3);
    private const int MaxTravelBufferMinutesEachSide = 60;

    private readonly IBookingTransactionRepository _txRepo;
    private readonly IBookingSlotRepository _slotRepo;
    private readonly IBookingHoldRepository _holdRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICalendarGateway _calendar;
    private readonly IClientDirectory _clients;
    private readonly IClock _clock;
    private readonly ILogger<CreateBookingHandler> _logger;

    public CreateBookingHandler(
        IBookingTransactionRepository txRepo,
        IBookingSlotRepository slotRepo,
        IBookingHoldRepository holdRepo,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IClientDirectory clients,
        IClock clock,
        ILogger<CreateBookingHandler> logger)
    {
        _txRepo = txRepo;
        _slotRepo = slotRepo;
        _holdRepo = holdRepo;
        _uow = uow;
        _calendar = calendar;
        _clients = clients;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CreateBookingResponse>> HandleAsync(CreateHoldCommand cmd, CancellationToken ct)
    {
        var validation = Validate(cmd);
        if (!validation.IsSuccess)
            return FailFrom<CreateBookingResponse>(validation);

        var utcNow = _clock.UtcNow;

        var loadResult = await LoadSlotAndTransactionAsync(cmd, ct);
        if (!loadResult.IsSuccess)
            return FailFrom<CreateBookingResponse>(loadResult);

        var (slot, tx) = loadResult.Value;

        var holdCheck = await EnsureNoActiveHoldAsync(slot, utcNow, ct);
        if (!holdCheck.IsSuccess)
            return FailFrom<CreateBookingResponse>(holdCheck);

        var holdCreate = CreateHold(slot, utcNow);
        if (!holdCreate.IsSuccess)
            return FailFrom<CreateBookingResponse>(holdCreate);

        var hold = holdCreate.Value;

        await _holdRepo.AddAsync(hold, ct);

        await _uow.SaveChangesAsync(ct);

        var calendarResult = await TryCreateCalendarHoldEventAsync(slot, tx, hold, ct);
        if (!calendarResult.IsSuccess)
            return FailFrom<CreateBookingResponse>(calendarResult);
    

        await _uow.SaveChangesAsync(ct);

        return Result<CreateBookingResponse>.Ok(new CreateBookingResponse
        {
            BookingId = hold.Id,
            SlotId = hold.SlotId,
            HoldExpiresUtc = hold.ExpiresUtc
        });
    }

    // -------------------------
    // Validation / Loading
    // -------------------------
    private static Result<Unit> Validate(CreateHoldCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.SlotId))
        {
            return Result<Unit>.Fail(
                HttpStatusCode.BadRequest,
                "slotId is required.",
                Errors.Validation);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private async Task<Result<(BookingSlot Slot, BookingTransaction Tx)>> LoadSlotAndTransactionAsync(
        CreateHoldCommand cmd,
        CancellationToken ct)
    {
        var slotId = cmd.SlotId.Trim();

        var slot = await _slotRepo.GetAsync(slotId, ct);
        if (slot is null)
            return Result<(BookingSlot, BookingTransaction)>.NotFound($"Slot '{cmd.SlotId}' not found.");

        var tx = await _txRepo.GetAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<(BookingSlot, BookingTransaction)>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' not found for slot '{cmd.SlotId}'.",
                Errors.Conflict);
        }

        if (!string.IsNullOrWhiteSpace(cmd.TransactionRef) &&
            !string.Equals(cmd.TransactionRef.Trim(), tx.TransactionRef, StringComparison.OrdinalIgnoreCase))
        {
            return Result<(BookingSlot, BookingTransaction)>.Fail(
                HttpStatusCode.Conflict,
                "slotId does not belong to the supplied transactionRef.",
                Errors.Conflict);
        }

        return Result<(BookingSlot, BookingTransaction)>.Ok((slot, tx));
    }

    private async Task<Result<Unit>> EnsureNoActiveHoldAsync(BookingSlot slot, DateTime utcNow, CancellationToken ct)
    {
        var existing = await _holdRepo.GetActiveBySlotIdAsync(slot.Id, utcNow, ct);
        if (existing is not null)
        {
            return Result<Unit>.Fail(
                HttpStatusCode.Conflict,
                "This slot is already on hold.",
                Errors.Conflict);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    // -------------------------
    // Domain creation
    // -------------------------
    private static Result<BookingHold> CreateHold(BookingSlot slot, DateTime utcNow)
    {
        try
        {
            var hold = BookingHold.Create(
                slotId: slot.Id,
                userId: slot.AdviserId,
                utcNow: utcNow,
                holdDuration: DefaultHoldWindow);

            return Result<BookingHold>.Ok(hold);
        }
        catch (DomainException ex)
        {
            return Result<BookingHold>.Fail(
                HttpStatusCode.BadRequest,
                ex.Message,
                Errors.Validation);
        }
    }

    // -------------------------
    // Calendar
    // -------------------------
    private async Task<Result<Unit>> TryCreateCalendarHoldEventAsync(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold hold,
        CancellationToken ct)
    {
        var windows = BuildHoldWindows(slot, tx);

        _logger.LogInformation(
            "Creating Graph hold event for HoldId={HoldId} SlotId={SlotId} AdviserId={AdviserId} SlotStartUtc={SlotStartUtc} SlotEndUtc={SlotEndUtc} HoldStartUtc={HoldStartUtc} HoldEndUtc={HoldEndUtc} TravelBufferMins={TravelBufferMins}",
            hold.Id, slot.Id, slot.AdviserId, slot.StartUtc, slot.EndUtc,
            windows.HoldStartUtc, windows.HoldEndUtc, windows.TravelBufferMinutesEachSide);

        var subject = BuildSubject(tx);

        var body = HoldBookingTemplate.BuildHoldBodyTemplate(slot, tx, hold, windows);

        CalendarLocation? calendarLocation = null;
        if (!tx.IsRemote)
        {
            var prospect = await _clients.GetAsync(tx.TransactionRef, ct);
            calendarLocation = CalendarLocation.CreateOrNull(
                displayName: prospect?.FullAddress,
                addressLine1: prospect?.StreetName1,
                city: prospect?.Town,
                postcode: prospect?.PostalCode);
        }

        BookingCalendarEvent calendarEvent;
        try
        {
            calendarEvent = BookingCalendarEvent.Create(
                userId: slot.AdviserId,
                externalId: $"hold:{hold.Id}",
                subject: subject,
                startUtc: windows.HoldStartUtc,
                endUtc: windows.HoldEndUtc,
                timezone: tx.Timezone,
                isRemote: tx.IsRemote,
                categories: new[] { "AFH Booking", "Hold" },
                body: body,
                providerEventId: hold.CalendarProviderEventId,
                location: tx.IsRemote ? null : calendarLocation,
                attendees: null,
                showAs: BookingShowAs.Tentative
            );
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Fail(
                HttpStatusCode.BadRequest,
                ex.Message,
                Errors.Validation);
        }

        var providerEventId = await _calendar.CreateBookingEventAsync(calendarEvent, ct);

        if (!string.IsNullOrWhiteSpace(providerEventId))
        {
            hold.AttachCalendarEvent(providerEventId);
            await _holdRepo.UpdateAsync(hold, ct);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private static string BuildSubject(BookingTransaction tx)
    {
        return string.IsNullOrWhiteSpace(tx.MeetingType)
            ? "AFH Booking"
            : $"AFH Booking - {tx.MeetingType}";
    }

    // -------------------------
    // Windows + Body template
    // -------------------------
    //private sealed record HoldWindows(
    //    DateTime HoldStartUtc,
    //    DateTime HoldEndUtc,
    //    int TravelBufferMinutesEachSide,
    //    bool TravelApplied);

    private HoldWindows BuildHoldWindows(BookingSlot slot, BookingTransaction tx)
    {
        // Remote: no travel buffer
        if (tx.IsRemote)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, false);

        var oneWayMinutes = slot?.TravelMinutes;

        if (oneWayMinutes is null || oneWayMinutes <= 0)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, false);

        var buffer = Math.Min(oneWayMinutes.Value, MaxTravelBufferMinutesEachSide);

        var start = slot.StartUtc.AddMinutes(-buffer);
        var end = slot.EndUtc.AddMinutes(buffer);

        // Guard: don’t invert times
        if (end <= start)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, false);

        return new HoldWindows(start, end, buffer, true);
    }



   
    // -------------------------
    // Tiny helper
    // -------------------------
    private sealed class Unit
    {
        public static Unit Value { get; } = new Unit();
        private Unit() { }
    }



    private static Result<TOut> FailFrom<TOut>(Result r) 
    {
        return Result<TOut>.Fail(r.StatusCode, r.ErrorMessage, r.ErrorCode);
    }
}
