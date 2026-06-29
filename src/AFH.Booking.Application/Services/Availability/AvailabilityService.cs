using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Application.Services.Bookings.Core;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Client;
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
    private readonly IBookingReferenceGenerator? _references;

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
        IAvailabilityResponseBuilder responseBuilder,
        IBookingReferenceGenerator? references = null)
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
        _references = references;
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

        var txResult = await CreateTransactionAsync(q, prospectResult.Value, slotStartsUtc[0], utcNow, ct);
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

    private async Task<(BookingTransaction? Value, Result<GetAvailabilityResponse>? Error)> CreateTransactionAsync(
        GetAvailabilityQuery q,
        ClientDirectoryItem? client,
        DateTime firstSlot,
        DateTime utcNow,
        CancellationToken ct)
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

            tx.AssignBookingReference(_references is null
                ? BookingReferenceFallback.CreateBookingReference(tx.Id)
                : await _references.GenerateBookingReferenceAsync(tx.Id, ct));

            tx.CaptureClientSnapshot(
                BuildClientName(client) ?? q.ClientName,
                client?.Email ?? q.ClientEmail,
                client?.StreetName1 ?? q.DestinationAddress?.Line1,
                client?.StreetName2,
                client?.Town ?? q.DestinationAddress?.Town,
                client?.County,
                client?.PostalCode ?? q.DestinationAddress?.Postcode);

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

    private static string? BuildClientName(ClientDirectoryItem? client)
    {
        if (client is null)
            return null;

        var first = string.IsNullOrWhiteSpace(client.FirstName) ? null : client.FirstName.Trim();
        var last = string.IsNullOrWhiteSpace(client.LastName) ? null : client.LastName.Trim();
        var value = string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
