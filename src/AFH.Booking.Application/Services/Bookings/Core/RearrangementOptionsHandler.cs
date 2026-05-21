using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

public sealed class RearrangementOptionsHandler : IRearrangementOptionsHandler
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IAvailabilityHandler _availability;

    public RearrangementOptionsHandler(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IAvailabilityHandler availability)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _availability = availability;
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

        var assignedQuery = new GetAvailabilityQuery
        {
            ClientId = tx.TransactionRef,
            TransactionId = tx.Id,
            PreferredStart = startUtc,
            Duration = durationMinutes,
            IsRemote = isRemote,
            MeetingType = meetingType,
            LocationRef = tx.LocationRef,
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
            TransactionId = tx.Id,
            PreferredStart = startUtc,
            Duration = durationMinutes,
            IsRemote = isRemote,
            MeetingType = meetingType,
            LocationRef = tx.LocationRef,
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
            Paging = new PageResultDto<object>
            {
                PageSize = 0,
                ReturnedCount = 0,
                NextCursor = null
            }
        };
}
