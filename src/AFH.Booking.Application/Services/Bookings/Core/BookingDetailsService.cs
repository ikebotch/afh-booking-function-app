using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Bookings;

public sealed class BookingDetailsService : IBookingDetailsService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IBookingTokenService _tokenService;
    private readonly NotificationsOptions _notificationOptions;

    public BookingDetailsService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IBookingTokenService tokenService,
        IOptions<NotificationsOptions> notificationOptions)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _tokenService = tokenService;
        _notificationOptions = notificationOptions.Value;
    }

    public async Task<Result<BookingDetailsResponse>> HandleAsync(GetBookingDetailsQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.BookingId))
        {
            return Result<BookingDetailsResponse>.Fail(
                HttpStatusCode.BadRequest,
                "bookingId is required.",
                Errors.Validation);
        }

        var hold = await _holds.GetAsync(query.BookingId.Trim(), ct);
        if (hold is null)
            return Result<BookingDetailsResponse>.NotFound($"Booking '{query.BookingId}' was not found.");

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
        {
            return Result<BookingDetailsResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' linked to booking was not found.",
                Errors.Conflict);
        }

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<BookingDetailsResponse>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' linked to booking was not found.",
                Errors.Conflict);
        }

        var links = await BuildSelfServiceLinksAsync(hold.Id, ct);

        var response = new BookingDetailsResponse
        {
            BookingId = hold.Id,
            SlotId = slot.Id,
            TransactionId = tx.Id,
            TransactionRef = tx.TransactionRef,
            AdviserId = slot.AdviserId,
            AdviserName = slot.AdviserName,
            StartUtc = slot.StartUtc,
            EndUtc = slot.EndUtc,
            DurationMinutes = (int)Math.Round((slot.EndUtc - slot.StartUtc).TotalMinutes),
            IsRemote = tx.IsRemote,
            MeetingType = tx.MeetingType,
            Status = hold.Status.ToString(),
            ConfirmedUtc = hold.ConfirmedUtc,
            CancelledUtc = hold.CancelledUtc,
            CancelReason = hold.CancelReason,
            ViewBookingUrl = links?.ViewBookingUrl,
            CancelBookingUrl = links?.CancelBookingUrl,
            RescheduleBookingUrl = links?.RescheduleBookingUrl
        };

        return Result<BookingDetailsResponse>.Ok(response);
    }

    private async Task<BookingSelfServiceLinks?> BuildSelfServiceLinksAsync(string bookingId, CancellationToken ct)
    {
        var tokenResult = await _tokenService.GenerateClientAccessTokenAsync(bookingId, ct);
        return tokenResult.IsSuccess
            ? BookingSelfServiceLinkBuilder.Build(_notificationOptions.ClientPortalBaseUrl, bookingId, tokenResult.Value)
            : null;
    }
}
