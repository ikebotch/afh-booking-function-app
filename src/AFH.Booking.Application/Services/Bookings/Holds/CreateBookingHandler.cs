

using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Holds;

public sealed class CreateBookingHandler : ICreateBookingHandler
{
    private readonly IBookingContextLoader _loader;
    private readonly IBookingHoldService _holdService;
    private readonly IBookingCalendarService _calendarService;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CreateBookingHandler(
        IBookingContextLoader loader,
        IBookingHoldService holdService,
        IBookingCalendarService calendarService,
        IUnitOfWork uow,
        IClock clock)
    {
        _loader = loader;
        _holdService = holdService;
        _calendarService = calendarService;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<CreateBookingResponse>> HandleAsync(
        CreateHoldCommand cmd,
        CancellationToken ct)
    {
        var validation = Validate(cmd);
        if (!validation.IsSuccess)
            return Result<CreateBookingResponse>.Fail(
                validation.StatusCode,
                validation.ErrorMessage,
                validation.ErrorCode);

        var utcNow = _clock.UtcNow;

        var contextResult = await _loader.LoadAsync(cmd, ct);
        if (!contextResult.IsSuccess)
            return Result<CreateBookingResponse>.Fail(
                contextResult.StatusCode,
                contextResult.ErrorMessage,
                contextResult.ErrorCode);

        var context = contextResult.Value;

        var holdResult = await _holdService.CreateOrReplaceAsync(
            context,
            utcNow,
            ct);

        if (!holdResult.IsSuccess)
            return Result<CreateBookingResponse>.Fail(
                holdResult.StatusCode,
                holdResult.ErrorMessage,
                holdResult.ErrorCode);

        var hold = holdResult.Value;

        var calendarResult = await _calendarService.CreateHoldEventAsync(
            context,
            hold,
            ct);

        if (!calendarResult.IsSuccess)
            return Result<CreateBookingResponse>.Fail(
                calendarResult.StatusCode,
                calendarResult.ErrorMessage,
                calendarResult.ErrorCode);

        await _uow.SaveChangesAsync(ct);

        return Result<CreateBookingResponse>.Ok(CreateResponse(
            hold,
            context.Transaction,
            context.Slot));
    }

    private static Result<Unit> Validate(CreateHoldCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.SlotId))
        {
            return Result<Unit>.Fail(
                System.Net.HttpStatusCode.BadRequest,
                "slotId is required.",
                Errors.Validation);
        }

        return Result<Unit>.Ok(Unit.Value);
    }

    private static CreateBookingResponse CreateResponse(
        BookingHold hold,
        BookingTransaction tx,
        BookingSlot slot)
    {
        return new CreateBookingResponse
        {
            BookingId = hold.Id,
            SlotId = hold.SlotId,
            HoldExpiresUtc = hold.ExpiresUtc,
            CompanyBufferMinutes = tx.IsRemote
                ? 0
                : Math.Max(0, slot.CompanyBufferMinutes ?? 30)
        };
    }
}


