using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Client;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Application.Bookings;

public sealed class BookingDetailsService : IBookingDetailsService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IAdviserProfileProjectionRepository _adviserProfiles;
    private readonly IBookingTokenService _tokenService;
    private readonly IClientDirectory? _clients;
    private readonly NotificationsOptions _notificationOptions;

    public BookingDetailsService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IBookingTokenService tokenService,
        IOptions<NotificationsOptions> notificationOptions)
        : this(
            holds,
            slots,
            transactions,
            NullAdviserProfileProjectionRepository.Instance,
            tokenService,
            null,
            notificationOptions)
    {
    }

    public BookingDetailsService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IAdviserProfileProjectionRepository adviserProfiles,
        IBookingTokenService tokenService,
        IClientDirectory? clients,
        IOptions<NotificationsOptions> notificationOptions)
    {
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _adviserProfiles = adviserProfiles;
        _tokenService = tokenService;
        _clients = clients;
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
        var canUseActionLinks = BookingSelfServiceStatusRules.CanUseActionLinks(hold.Status);
        var adviserProfile = await _adviserProfiles.GetAsync(slot.AdviserId, ct);
        var client = await TryGetClientAsync(tx.TransactionRef, ct);

        var response = new BookingDetailsResponse
        {
            BookingId = hold.Id,
            BookingReference = tx.BookingReference ?? hold.Reference,
            SlotId = slot.Id,
            TransactionId = tx.Id,
            TransactionRef = tx.TransactionRef,
            ClientRef = tx.TransactionRef,
            ClientName = tx.ClientName ?? BuildClientName(client),
            ClientEmail = tx.ClientEmail ?? client?.Email,
            ClientAddressLine1 = tx.ClientAddressLine1 ?? client?.StreetName1,
            ClientAddressLine2 = tx.ClientAddressLine2 ?? client?.StreetName2,
            ClientTown = tx.ClientTown ?? client?.Town,
            ClientCounty = tx.ClientCounty ?? client?.County,
            ClientPostcode = tx.ClientPostcode ?? client?.PostalCode,
            AdviserId = slot.AdviserId,
            AdviserName = slot.AdviserName,
            AdviserRegion = adviserProfile?.Region,
            StartUtc = slot.StartUtc,
            EndUtc = slot.EndUtc,
            DurationMinutes = (int)Math.Round((slot.EndUtc - slot.StartUtc).TotalMinutes),
            IsRemote = tx.IsRemote,
            MeetingType = tx.MeetingType,
            LocationRef = slot.LocationRef ?? tx.LocationRef,
            Status = hold.Status.ToString(),
            ConfirmedUtc = hold.ConfirmedUtc,
            CancelledUtc = hold.CancelledUtc,
            CancelReason = hold.CancelReason,
            ViewBookingUrl = links?.ViewBookingUrl,
            CancelBookingUrl = canUseActionLinks ? links?.CancelBookingUrl : null,
            RescheduleBookingUrl = canUseActionLinks ? links?.RescheduleBookingUrl : null
        };

        return Result<BookingDetailsResponse>.Ok(response);
    }

    private async Task<ClientDirectoryItem?> TryGetClientAsync(string transactionRef, CancellationToken ct)
    {
        if (_clients is null || string.IsNullOrWhiteSpace(transactionRef))
            return null;

        try
        {
            return await _clients.GetAsync(transactionRef, ct);
        }
        catch
        {
            return null;
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

    private async Task<BookingSelfServiceLinks?> BuildSelfServiceLinksAsync(string bookingId, CancellationToken ct)
    {
        var tokenResult = await _tokenService.GenerateClientAccessTokenAsync(bookingId, ct);
        return tokenResult.IsSuccess
            ? BookingSelfServiceLinkBuilder.Build(_notificationOptions.ClientPortalBaseUrl, bookingId, tokenResult.Value)
            : null;
    }

    private sealed class NullAdviserProfileProjectionRepository : IAdviserProfileProjectionRepository
    {
        public static readonly NullAdviserProfileProjectionRepository Instance = new();

        public Task UpsertRangeAsync(IReadOnlyList<Models.AdviserProjection.AdviserProfileProjectionRecord> advisers, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<Models.AdviserProjection.AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Models.AdviserProjection.AdviserProfileProjectionRecord>>([]);

        public Task<IReadOnlyList<Models.AdviserProjection.AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Models.AdviserProjection.AdviserProfileProjectionRecord>>([]);

        public Task<Models.AdviserProjection.AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
            => Task.FromResult<Models.AdviserProjection.AdviserProfileProjectionRecord?>(null);
    }
}
