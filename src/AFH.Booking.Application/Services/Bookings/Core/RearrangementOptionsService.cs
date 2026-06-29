using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Models.Availability;
using AFH.Booking.Application.Models.Common;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Location;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangementOptionsService : IRearrangementOptionsService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IAvailabilityService _availability;
    private readonly ILogger<RearrangementOptionsService> _logger;

    public RearrangementOptionsService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IAvailabilityService availability,
        ILogger<RearrangementOptionsService>? logger = null)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _availability = availability;
        _logger = logger ?? NullLogger<RearrangementOptionsService>.Instance;
    }

    public async Task<Result<RearrangementOptionsResponse>> HandleAsync(GetRearrangementOptionsCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.BookingId))
        {
            return Result<RearrangementOptionsResponse>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);
        }

        var hold = await _holds.GetAsync(cmd.BookingId.Trim(), ct);
        if (hold is null)
            return Result<RearrangementOptionsResponse>.NotFound($"Booking '{cmd.BookingId}' was not found.");

        var actionable = BookingSelfServiceStatusRules.EnsureActionable(hold, "rearranged");
        if (!actionable.IsSuccess)
        {
            return Result<RearrangementOptionsResponse>.Fail(
                actionable.StatusCode,
                actionable.ErrorMessage ?? "Booking cannot be rearranged.",
                actionable.ErrorCode);
        }

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
        {
            return Result<RearrangementOptionsResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' linked to booking was not found.",
                Errors.Conflict);
        }

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<RearrangementOptionsResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' linked to booking was not found.",
                Errors.Conflict);
        }

        var startUtc = ParsePreferredStartOrDefault(cmd.PreferredStartUtc, slot.StartUtc);
        var durationMinutes = cmd.Duration.GetValueOrDefault((int)Math.Round(tx.Duration.TotalMinutes));
        var isRemote = cmd.IsRemote ?? tx.IsRemote;
        var meetingType = string.IsNullOrWhiteSpace(cmd.MeetingType) ? tx.MeetingType ?? "Review" : cmd.MeetingType;
        var limit = cmd.Limit.GetValueOrDefault(10);
        var destinationAddress = BuildDestinationAddress(tx);
        var clientLookupRef = ResolveClientLookupRef(tx, isRemote, destinationAddress);
        if (!isRemote &&
            string.IsNullOrWhiteSpace(clientLookupRef) &&
            !HasCompleteDestinationAddress(destinationAddress))
        {
            return Result<RearrangementOptionsResponse>.Fail(
                HttpStatusCode.BadRequest,
                "Original booking transaction reference or captured client address is required for in-person rearrangement options.",
                Errors.Validation);
        }

        LogRearrangementContext(hold.Id, tx.TransactionRef, !isRemote, clientLookupRef is null ? "NotRequiredRemote" : "OriginalBookingTransactionRef");

        var assignedQuery = new GetAvailabilityQuery
        {
            ClientId = tx.TransactionRef,
            ClientName = tx.ClientName,
            ClientEmail = tx.ClientEmail,
            TransactionId = tx.Id,
            ClientLookupRef = clientLookupRef,
            ClientLookupSource = clientLookupRef is null ? null : "OriginalBookingTransactionRef",
            PreferredStart = startUtc,
            Duration = durationMinutes,
            IsRemote = isRemote,
            MeetingType = meetingType,
            LocationRef = tx.LocationRef,
            DestinationAddress = destinationAddress,
            PreferredAdviserIds = new[] { slot.AdviserId },
            Limit = limit,
            Take = limit,
            Cursor = cmd.Cursor
        };

        var assignedResult = await _availability.HandleAsync(assignedQuery, ct);
        if (!assignedResult.IsSuccess)
        {
            return Result<RearrangementOptionsResponse>.Fail(
                assignedResult.StatusCode,
                assignedResult.ErrorMessage ?? "Unable to get assigned adviser availability.",
                assignedResult.ErrorCode);
        }

        var alternativeQuery = new GetAvailabilityQuery
        {
            ClientId = tx.TransactionRef,
            ClientName = tx.ClientName,
            ClientEmail = tx.ClientEmail,
            TransactionId = tx.Id,
            ClientLookupRef = clientLookupRef,
            ClientLookupSource = clientLookupRef is null ? null : "OriginalBookingTransactionRef",
            PreferredStart = startUtc,
            Duration = durationMinutes,
            IsRemote = isRemote,
            MeetingType = meetingType,
            LocationRef = tx.LocationRef,
            DestinationAddress = destinationAddress,
            ExcludeAdviserIds = new[] { slot.AdviserId },
            Limit = limit,
            Take = limit,
            Cursor = cmd.Cursor
        };

        var alternativesResult = await _availability.HandleAsync(alternativeQuery, ct);
        if (!alternativesResult.IsSuccess)
        {
            return Result<RearrangementOptionsResponse>.Fail(
                alternativesResult.StatusCode,
                alternativesResult.ErrorMessage ?? "Unable to get alternative adviser availability.",
                alternativesResult.ErrorCode);
        }

        var assignedOptions = assignedResult.Value ?? EmptyAvailability();
        var alternativeOptions = alternativesResult.Value ?? EmptyAvailability();

        return Result<RearrangementOptionsResponse>.Ok(new RearrangementOptionsResponse
        {
            BookingId = hold.Id,
            BookingReference = tx.BookingReference ?? hold.Reference,
            TransactionId = tx.Id,
            AssignedAdviserId = slot.AdviserId,
            AssignedAdviserName = slot.AdviserName,
            AssignedAdviserHasAvailability = assignedOptions.Advisers.Count > 0,
            AssignedAdviserOptions = assignedOptions,
            AlternativeAdviserOptions = alternativeOptions
        });
    }

    private static DateTime ParsePreferredStartOrDefault(string? preferredStartUtc, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(preferredStartUtc))
            return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);

        if (DateTimeOffset.TryParse(preferredStartUtc, out var parsed))
            return DateTime.SpecifyKind(parsed.UtcDateTime, DateTimeKind.Utc);

        if (DateOnly.TryParse(preferredStartUtc, out var date))
            return DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
    }

    private static GetAvailabilityResponse EmptyAvailability()
        => new()
        {
            Advisers = new(),
            Paging = new PageResult<object>
            {
                PageSize = 0,
                ReturnedCount = 0,
                NextCursor = null
            }
        };

    private static string? ResolveClientLookupRef(BookingTransaction tx, bool isRemote, LocationAddress? destinationAddress)
        => isRemote || HasCompleteDestinationAddress(destinationAddress) ? null : tx.TransactionRef;

    private static LocationAddress? BuildDestinationAddress(BookingTransaction tx)
    {
        if (string.IsNullOrWhiteSpace(tx.ClientAddressLine1) ||
            string.IsNullOrWhiteSpace(tx.ClientTown) ||
            string.IsNullOrWhiteSpace(tx.ClientPostcode))
        {
            return null;
        }

        return new LocationAddress
        {
            Line1 = tx.ClientAddressLine1,
            Town = tx.ClientTown,
            Postcode = tx.ClientPostcode,
            Country = "UK"
        };
    }

    private static bool HasCompleteDestinationAddress(LocationAddress? address)
        => address is not null &&
           !string.IsNullOrWhiteSpace(address.Line1) &&
           !string.IsNullOrWhiteSpace(address.Town) &&
           !string.IsNullOrWhiteSpace(address.Postcode);

    private void LogRearrangementContext(
        string bookingId,
        string? bookingTransactionRef,
        bool lookupAttempted,
        string lookupSource)
    {
        _logger.LogInformation(
            "Booking in-person rearrangement context. BookingId={BookingId} BookingTransactionRefHash={BookingTransactionRefHash} ClientLookupAttempted={ClientLookupAttempted} LookupSource={LookupSource}",
            bookingId,
            HashForLog(bookingTransactionRef),
            lookupAttempted,
            lookupSource);
    }

    private static string? HashForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes)[..12];
    }
}
