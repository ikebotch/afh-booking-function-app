using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IBookingTransactionRepository _txRepo;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly IProspectResolver _prospectResolver;
    private readonly IAvailabilityTransactionGuard _transactionGuard;
    private readonly ISlotStartBuilder _slotStartBuilder;
    private readonly IAdviserPoolBuilder _adviserPoolBuilder;
    private readonly IAvailabilitySlotProcessor _slotProcessor;
    private readonly IAvailabilityResponseBuilder _responseBuilder;

    public AvailabilityService(
        IBookingTransactionRepository txRepo,
        IUnitOfWork uow,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        IProspectResolver prospectResolver,
        IAvailabilityTransactionGuard transactionGuard,
        ISlotStartBuilder slotStartBuilder,
        IAdviserPoolBuilder adviserPoolBuilder,
        IAvailabilitySlotProcessor slotProcessor,
        IAvailabilityResponseBuilder responseBuilder)
    {
        _txRepo = txRepo;
        _uow = uow;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _prospectResolver = prospectResolver;
        _transactionGuard = transactionGuard;
        _slotStartBuilder = slotStartBuilder;
        _adviserPoolBuilder = adviserPoolBuilder;
        _slotProcessor = slotProcessor;
        _responseBuilder = responseBuilder;
    }

    public async Task<Result<GetAvailabilityResponse>> HandleAsync(GetAvailabilityQuery q, CancellationToken ct)
    {
        if (!ValidateQuery(q, out var error))
            return error!;

        var utcNow = _clock.UtcNow;

        var prospectResult = await _prospectResolver.ResolveAsync(q, ct);
        if (prospectResult.Error is not null)
            return prospectResult.Error;

        var closedResult = await _transactionGuard.EnsureOpenAsync(q, ct);
        if (closedResult is not null)
            return closedResult;

        var (slotStartsUtc, nextCursor) = _slotStartBuilder.BuildPage(q);
        if (slotStartsUtc.Count == 0)
            return _responseBuilder.Empty(nextCursor);

        var txResult = CreateTransaction(q, slotStartsUtc[0], utcNow);
        if (txResult.Error is not null)
            return txResult.Error;

        var tx = txResult.Value!;
        await _txRepo.AddAsync(tx, ct);

        var adviserPoolResult = await _adviserPoolBuilder.BuildAsync(q, prospectResult.Value, ct);
        if (adviserPoolResult.Error is not null)
            return adviserPoolResult.Error;

        var advisers = adviserPoolResult.Value.Advisers;
        if (advisers.Count == 0)
            return _responseBuilder.Empty(nextCursor);

        var adviserSlots = await _slotProcessor.ProcessAsync(
            q,
            advisers,
            slotStartsUtc,
            tx,
            adviserPoolResult.Value.TravelByAdviserId,
            utcNow,
            ct);

        await _uow.SaveChangesAsync(ct);

        return _responseBuilder.Success(q, tx.Id, adviserSlots, nextCursor);
    }

    private static bool ValidateQuery(GetAvailabilityQuery q, out Result<GetAvailabilityResponse>? errorResult)
    {
        errorResult = null;

        if (string.IsNullOrWhiteSpace(q.TransactionId) && string.IsNullOrWhiteSpace(q.ClientId))
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "Either transactionId or clientId must be provided.",
                Errors.Validation);
            return false;
        }

        if (q.Duration <= 0)
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "duration must be > 0.",
                Errors.Validation);
            return false;
        }

        if (q.PreferredStart == default)
        {
            errorResult = Result<GetAvailabilityResponse>.Fail(
                HttpStatusCode.BadRequest,
                "proposedStartUtc is required.",
                Errors.Validation);
            return false;
        }

        return true;
    }

    private (BookingTransaction? Value, Result<GetAvailabilityResponse>? Error) CreateTransaction(
        GetAvailabilityQuery q,
        DateTime firstSlot,
        DateTime utcNow)
    {
        try
        {
            var tx = BookingTransaction.Create(
                transactionRef: q.TransactionId ?? q.ClientId!,
                proposedStartUtc: firstSlot,
                duration: TimeSpan.FromMinutes(q.Duration),
                timezone: _timeZoneProvider.DefaultTimeZoneId,
                isRemote: q.IsRemote,
                meetingType: q.MeetingType,
                locationRef: q.LocationRef,
                utcNow: utcNow,
                expiresUtc: utcNow.AddMinutes(10));

            return (tx, null);
        }
        catch (DomainException ex)
        {
            return (null,
                Result<GetAvailabilityResponse>.Fail(
                    HttpStatusCode.BadRequest,
                    ex.Message,
                    Errors.Validation));
        }
    }
}
