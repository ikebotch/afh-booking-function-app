using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Holds;

public sealed class CreateBookingHandler : ICreateBookingHandler
{
    private const int DefaultCompanyBufferMinutes = 30;
    private static readonly TimeSpan DefaultHoldWindow = TimeSpan.FromMinutes(3);

    private readonly IBookingTransactionRepository _txRepo;
    private readonly IBookingSlotRepository _slotRepo;
    private readonly IBookingHoldRepository _holdRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IClientDirectory _clients;
    private readonly IClock _clock;
    private readonly ILogger<CreateBookingHandler> _logger;

    public CreateBookingHandler(
        IBookingTransactionRepository txRepo,
        IBookingSlotRepository slotRepo,
        IBookingHoldRepository holdRepo,
        IUnitOfWork uow,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IClientDirectory clients,
        IClock clock,
        ILogger<CreateBookingHandler> logger)
    {
        _txRepo = txRepo;
        _slotRepo = slotRepo;
        _holdRepo = holdRepo;
        _uow = uow;
        _calendar = calendar;
        _profiles = profiles;
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
        var activeHolds = await _holdRepo.GetActiveForCreateHoldAsync(slot.TransactionId, slot.Id, utcNow, ct);
        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(slot.AdviserId, ct);

        if (activeHolds.TransactionHold is not null &&
            string.Equals(activeHolds.TransactionHold.SlotId, slot.Id, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateBookingResponse>.Ok(BuildResponse(activeHolds.TransactionHold, slot, tx));
        }

        if (activeHolds.TransactionHold is not null &&
            activeHolds.SlotHold is not null &&
            !string.Equals(activeHolds.TransactionHold.Id, activeHolds.SlotHold.Id, StringComparison.OrdinalIgnoreCase))
        {
            var staleSlotHoldResult = await CancelStaleSlotHoldAsync(slot, tx, activeHolds.SlotHold, utcNow, ct);
            if (!staleSlotHoldResult.IsSuccess)
                return FailFrom<CreateBookingResponse>(staleSlotHoldResult);
        }
        else
        {
            var holdCheck = EnsureNoActiveHold(activeHolds.TransactionHold, activeHolds.SlotHold);
            if (!holdCheck.IsSuccess)
                return FailFrom<CreateBookingResponse>(holdCheck);
        }

        var availabilityCheck = await EnsureFreshAvailabilityAsync(
            slot,
            tx,
            calendarUserId,
            activeHolds.TransactionHold,
            ct);
        if (!availabilityCheck.IsSuccess)
            return FailFrom<CreateBookingResponse>(availabilityCheck);

        if (activeHolds.TransactionHold is not null)
        {
            var moveResult = await MoveExistingHoldAsync(slot, tx, activeHolds.TransactionHold, calendarUserId, utcNow, ct);
            if (!moveResult.IsSuccess)
                return moveResult;

            return Result<CreateBookingResponse>.Ok(BuildResponse(activeHolds.TransactionHold, slot, tx));
        }

        var holdCreate = CreateHold(slot, calendarUserId, utcNow);
        if (!holdCreate.IsSuccess)
            return FailFrom<CreateBookingResponse>(holdCreate);

        var hold = holdCreate.Value;

        await _holdRepo.AddAsync(hold, ct);

        await _uow.SaveChangesAsync(ct);

        var calendarResult = await TryCreateCalendarHoldEventAsync(slot, tx, hold, calendarUserId, ct);
        if (!calendarResult.IsSuccess)
            return FailFrom<CreateBookingResponse>(calendarResult);


        await _uow.SaveChangesAsync(ct);

        return Result<CreateBookingResponse>.Ok(BuildResponse(hold, slot, tx));
    }

    private static CreateBookingResponse BuildResponse(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction tx) => new()
    {
        BookingId = hold.Id,
        SlotId = hold.SlotId,
        HoldExpiresUtc = hold.ExpiresUtc,
        CompanyBufferMinutes = tx.IsRemote
            ? 0
            : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes)
    };

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

    private async Task<Result<CreateBookingResponse>> MoveExistingHoldAsync(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold existing,
        string calendarUserId,
        DateTime utcNow,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(existing.CalendarProviderEventId))
        {
            try
            {
                var existingCalendarUserId = existing.UserId.Contains('@')
                    ? existing.UserId
                    : await _profiles.ResolveCalendarUserIdAsync(existing.UserId, ct);
                await _calendar.CancelBookingEventAsync(
                    existingCalendarUserId,
                    existing.CalendarProviderEventId,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to remove existing calendar hold marker for BookingId={BookingId} TransactionId={TransactionId} SlotId={SlotId}",
                    existing.Id,
                    tx.Id,
                    existing.SlotId);

                return Result<CreateBookingResponse>.Fail(
                    HttpStatusCode.Conflict,
                    "Unable to remove the existing booking hold marker before re-checking availability.",
                    Errors.Conflict);
            }
        }

        try
        {
            existing.MoveToSlot(slot.Id, calendarUserId, DefaultHoldWindow, utcNow);
        }
        catch (DomainException ex)
        {
            return Result<CreateBookingResponse>.Fail(HttpStatusCode.BadRequest, ex.Message, Errors.Validation);
        }

        await _holdRepo.UpdateAsync(existing, ct);
        await _uow.SaveChangesAsync(ct);

        var calendarResult = await TryCreateCalendarHoldEventAsync(slot, tx, existing, calendarUserId, ct);
        if (!calendarResult.IsSuccess)
            return FailFrom<CreateBookingResponse>(calendarResult);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Moved existing active hold to a new slot. HoldId={HoldId} TransactionId={TransactionId} NewSlotId={NewSlotId}",
            existing.Id,
            tx.Id,
            slot.Id);

        return Result<CreateBookingResponse>.Ok(BuildResponse(existing, slot, tx));
    }

    private async Task<Result<Unit>> CancelStaleSlotHoldAsync(
        BookingSlot slot,
        BookingTransaction tx,
        BookingHold staleHold,
        DateTime utcNow,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(staleHold.CalendarProviderEventId))
        {
            try
            {
                var staleCalendarUserId = staleHold.UserId.Contains('@')
                    ? staleHold.UserId
                    : await _profiles.ResolveCalendarUserIdAsync(staleHold.UserId, ct);

                await _calendar.CancelBookingEventAsync(
                    staleCalendarUserId,
                    staleHold.CalendarProviderEventId,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to remove stale calendar hold marker before moving current hold. StaleHoldId={StaleHoldId} TransactionId={TransactionId} SlotId={SlotId}",
                    staleHold.Id,
                    tx.Id,
                    slot.Id);

                return Result<Unit>.Fail(
                    HttpStatusCode.Conflict,
                    "Unable to remove the previous hold marker for this slot before moving the current hold.",
                    Errors.Conflict);
            }
        }

        staleHold.Cancel("Superseded by current hold move.", utcNow);
        await _holdRepo.UpdateAsync(staleHold, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<Unit>.Ok(Unit.Value);
    }

    private static Result<Unit> EnsureNoActiveHold(BookingHold? transactionHold, BookingHold? slotHold)
    {
        if (slotHold is not null &&
            (transactionHold is null || !string.Equals(transactionHold.Id, slotHold.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Unit>.Fail(
                HttpStatusCode.Conflict,
                "This slot is already on hold.",
                Errors.Conflict);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private async Task<Result<Unit>> EnsureFreshAvailabilityAsync(
        BookingSlot slot,
        BookingTransaction tx,
        string calendarUserId,
        BookingHold? movingHold,
        CancellationToken ct)
    {
        var windows = BuildHoldWindows(slot, tx);

        var availability = await _calendar.CheckAvailabilityAsync(
            calendarUserId,
            windows.HoldStartUtc,
            windows.HoldEndUtc,
            tx.Timezone,
            "ForceRefresh",
            ct);

        if (availability.MailboxUnavailable)
        {
            _logger.LogWarning(
                "Fresh hold validation could not verify mailbox availability for TransactionId={TransactionId} SlotId={SlotId} AdviserId={AdviserId} StartUtc={StartUtc} EndUtc={EndUtc}. Message={Message}",
                tx.Id,
                slot.Id,
                slot.AdviserId,
                windows.HoldStartUtc,
                windows.HoldEndUtc,
                availability.StatusMessage);

            return Result<Unit>.Fail(
                HttpStatusCode.Conflict,
                string.IsNullOrWhiteSpace(availability.StatusMessage)
                    ? "Unable to verify current adviser availability."
                    : availability.StatusMessage,
                Errors.Conflict);
        }

        var relevantConflicts = availability.Conflicts
            .Where(conflict => IsRelevantCalendarConflict(conflict, movingHold))
            .ToList();

        if (!availability.IsFree && (availability.Conflicts.Count == 0 || relevantConflicts.Count > 0))
        {
            return Result<Unit>.Fail(
                HttpStatusCode.Conflict,
                string.IsNullOrWhiteSpace(availability.StatusMessage)
                    ? "The requested slot is no longer available."
                    : availability.StatusMessage,
                Errors.BookingConflictDoubleBooked);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private static bool IsRelevantCalendarConflict(CalendarConflictBlock conflict, BookingHold? movingHold)
    {
        if (movingHold is null || string.IsNullOrWhiteSpace(movingHold.CalendarProviderEventId))
            return true;

        return !string.Equals(
            conflict.ProviderEventId,
            movingHold.CalendarProviderEventId,
            StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------
    // Domain creation
    // -------------------------
    private static Result<BookingHold> CreateHold(BookingSlot slot, string calendarUserId, DateTime utcNow)
    {
        try
        {
            var hold = BookingHold.Create(
                slotId: slot.Id,
                userId: calendarUserId,
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
        string calendarUserId,
        CancellationToken ct)
    {
        var windows = BuildHoldWindows(slot, tx);

        _logger.LogInformation(
            "Creating calendar hold event for HoldId={HoldId} SlotId={SlotId} AdviserId={AdviserId} SlotStartUtc={SlotStartUtc} SlotEndUtc={SlotEndUtc} HoldStartUtc={HoldStartUtc} HoldEndUtc={HoldEndUtc} TravelBufferMins={TravelBufferMins} CompanyBufferMins={CompanyBufferMins}",
            hold.Id, slot.Id, slot.AdviserId, slot.StartUtc, slot.EndUtc,
            windows.HoldStartUtc, windows.HoldEndUtc, windows.TravelBufferMinutesEachSide, windows.CompanyBufferMinutes);

        var subject = BuildSubject(tx);

        var calendarTemplate = HoldBookingTemplate.BuildHoldTemplate(slot, tx, hold, windows);

        CalendarLocation? calendarLocation = null;
        if (!tx.IsRemote)
        {
            var prospect = await _clients.GetAsync(tx.TransactionRef, ct);
            calendarLocation = CalendarLocation.CreateOrNull(
                displayName: BuildDisplayAddress(prospect),
                addressLine1: prospect?.StreetName1,
                city: prospect?.Town,
                postcode: prospect?.PostalCode);
        }

        BookingCalendarEvent calendarEvent;
        try
        {
            calendarEvent = BookingCalendarEvent.Create(
                userId: calendarUserId,
                externalId: $"hold:{hold.Id}",
                subject: subject,
                startUtc: windows.HoldStartUtc,
                endUtc: windows.HoldEndUtc,
                timezone: tx.Timezone,
                isRemote: tx.IsRemote,
                categories: new[] { "AFH Booking", "Hold" },
                body: calendarTemplate.CalendarDescription,
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

    private static string? BuildDisplayAddress(ClientDirectoryItem? client)
    {
        if (client is null) return null;

        var parts = new[]
        {
            client.StreetName1,
            client.StreetName2,
            client.Town,
            client.County,
            client.PostalCode
        }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p!.Trim());

        var value = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
        var travelMinutes = tx.IsRemote ? 0 : Math.Max(0, slot.TravelMinutes ?? 0);
        var companyBufferMinutes = tx.IsRemote
            ? 0
            : Math.Max(0, slot.CompanyBufferMinutes ?? DefaultCompanyBufferMinutes);

        var preMeetingMinutes = travelMinutes + companyBufferMinutes;
        var postMeetingMinutes = companyBufferMinutes;

        var start = slot.StartUtc.AddMinutes(-preMeetingMinutes);
        var end = slot.EndUtc.AddMinutes(postMeetingMinutes);

        // Guard: don’t invert times
        if (end <= start)
            return new HoldWindows(slot.StartUtc, slot.EndUtc, 0, 0, false);

        return new HoldWindows(start, end, travelMinutes, companyBufferMinutes, preMeetingMinutes > 0 || postMeetingMinutes > 0);
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
