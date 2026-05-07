using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Domain.Bookings;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AFH.Booking.Infrastructure.Bookings;

public sealed class HmacBookingChangeAccessService : IBookingChangeAccessService
{
    private readonly BookingChangeAccessOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _transactions;
    private readonly IBookingAccessLinkRepository _links;

    public HmacBookingChangeAccessService(
        IOptions<BookingChangeAccessOptions> options,
        IHostEnvironment hostEnvironment,
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository transactions,
        IBookingAccessLinkRepository links)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _holds = holds;
        _slots = slots;
        _transactions = transactions;
        _links = links;
    }

    public async Task<Result<BookingChangeActorContext>> ValidateClientTokenAsync(
        string bookingId,
        string? token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Unauthorized, "Client access token is required.", Errors.Unauthorized);

        if (_hostEnvironment.IsDevelopment() && _options.AllowUnsignedTokensInDevelopment)
        {
            return Result<BookingChangeActorContext>.Ok(new BookingChangeActorContext(LifecycleActors.Client, "dev-client", null));
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.InternalServerError, "BookingChangeAccess:SigningKey is required.", Errors.ServerError);

        if (!TryParseToken(token, out var envelope, out var error))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Unauthorized, error!, Errors.Unauthorized);

        if (!string.Equals(envelope!.BookingId, bookingId, StringComparison.Ordinal))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client token does not match booking.", Errors.Unauthorized);

        if (!string.Equals(envelope.ActorType, LifecycleActors.Client, StringComparison.OrdinalIgnoreCase))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client token actor is invalid.", Errors.Unauthorized);

        if (envelope.ExpiresUtc <= DateTimeOffset.UtcNow)
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Unauthorized, "Client token has expired.", Errors.Unauthorized);

        if (!ValidateSignature(token, _options.SigningKey!))
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client token signature is invalid.", Errors.Unauthorized);

        var targetBookingId = bookingId;
        if (!string.IsNullOrWhiteSpace(envelope.LinkId))
        {
            var link = await _links.GetAsync(envelope.LinkId, ct);
            if (link is null)
                return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client access link is invalid.", Errors.Unauthorized);

            if (!string.Equals(link.TokenHash, HashToken(token), StringComparison.Ordinal))
                return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client access link is invalid.", Errors.Unauthorized);

            if (link.RevokedUtc.HasValue)
                return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client access link has been revoked.", Errors.Unauthorized);

            if (DateTime.SpecifyKind(link.ExpiresUtc, DateTimeKind.Utc) <= DateTime.UtcNow)
                return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Unauthorized, "Client access link has expired.", Errors.Unauthorized);

            if (!string.Equals(link.OriginalBookingId, bookingId, StringComparison.Ordinal) &&
                !string.Equals(link.CurrentBookingId, bookingId, StringComparison.Ordinal))
            {
                return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client token does not match booking.", Errors.Unauthorized);
            }

            targetBookingId = link.CurrentBookingId;
        }

        var hold = await _holds.GetAsync(targetBookingId, ct);
        if (hold is null)
            return Result<BookingChangeActorContext>.NotFound($"Booking '{targetBookingId}' was not found.");

        if (hold.Status == BookingHoldStatus.Cancelled)
        {
            return Result<BookingChangeActorContext>.Fail(
                HttpStatusCode.Conflict,
                "Client access link can no longer be used because the booking has been cancelled.",
                Errors.Conflict);
        }

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' linked to booking was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' linked to booking was not found.", Errors.Conflict);

        if (!string.IsNullOrWhiteSpace(envelope.TransactionRef) &&
            !string.Equals(envelope.TransactionRef, tx.TransactionRef, StringComparison.OrdinalIgnoreCase))
        {
            return Result<BookingChangeActorContext>.Fail(HttpStatusCode.Forbidden, "Client token transaction does not match booking.", Errors.Unauthorized);
        }

        return Result<BookingChangeActorContext>.Ok(new BookingChangeActorContext(
            LifecycleActors.Client,
            envelope.ActorId,
            tx.TransactionRef,
            envelope.CorrelationId,
            targetBookingId));
    }

    public async Task<Result<BookingAccessLinkResponse>> CreateClientLinkAsync(
        CreateBookingAccessLinkRequest request,
        CancellationToken ct)
    {
        return await CreateClientLinkCoreAsync(request, revokeExisting: false, ct);
    }

    public async Task<Result<BookingAccessLinkResponse>> ResendClientLinkAsync(
        CreateBookingAccessLinkRequest request,
        CancellationToken ct)
    {
        return await CreateClientLinkCoreAsync(request, revokeExisting: true, ct);
    }

    private async Task<Result<BookingAccessLinkResponse>> CreateClientLinkCoreAsync(
        CreateBookingAccessLinkRequest request,
        bool revokeExisting,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            return Result<BookingAccessLinkResponse>.Fail(HttpStatusCode.InternalServerError, "BookingChangeAccess:SigningKey is required.", Errors.ServerError);

        var bookingId = request.BookingId.Trim();
        var hold = await _holds.GetAsync(bookingId, ct);
        if (hold is null)
            return Result<BookingAccessLinkResponse>.NotFound($"Booking '{bookingId}' was not found.");

        if (hold.Status == BookingHoldStatus.Cancelled)
            return Result<BookingAccessLinkResponse>.Fail(HttpStatusCode.Conflict, "A client link cannot be created for a cancelled booking.", Errors.Conflict);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
            return Result<BookingAccessLinkResponse>.Fail(HttpStatusCode.Conflict, $"Slot '{hold.SlotId}' linked to booking was not found.", Errors.Conflict);

        var tx = await _transactions.GetAsync(slot.TransactionId, ct);
        if (tx is null)
            return Result<BookingAccessLinkResponse>.Fail(HttpStatusCode.Conflict, $"Transaction '{slot.TransactionId}' linked to booking was not found.", Errors.Conflict);

        var utcNow = DateTime.UtcNow;
        if (revokeExisting)
            await _links.RevokeActiveForBookingAsync(bookingId, utcNow, "Client link resent", ct);

        var linkId = Guid.NewGuid().ToString("N");
        var expiresUtc = new DateTimeOffset(utcNow).AddHours(ResolveExpiryHours(request.ExpiryHours));
        var envelope = new BookingChangeAccessTokenEnvelope(
            bookingId,
            LifecycleActors.Client,
            expiresUtc,
            request.ActorId,
            tx.TransactionRef,
            Guid.NewGuid().ToString("N"),
            LinkId: linkId);

        var token = CreateToken(envelope, _options.SigningKey!);
        await _links.AddAsync(new BookingAccessLinkRecord
        {
            Id = linkId,
            OriginalBookingId = bookingId,
            CurrentBookingId = bookingId,
            TokenHash = HashToken(token),
            ActorType = LifecycleActors.Client,
            ActorId = request.ActorId,
            TransactionRef = tx.TransactionRef,
            ExpiresUtc = expiresUtc.UtcDateTime,
            CreatedUtc = utcNow,
            CreatedBy = request.CreatedBy
        }, ct);

        return Result<BookingAccessLinkResponse>.Ok(new BookingAccessLinkResponse
        {
            LinkId = linkId,
            BookingId = bookingId,
            AccessToken = token,
            AccessUrl = BuildAccessUrl(bookingId, token),
            ExpiresUtc = expiresUtc,
            TransactionRef = tx.TransactionRef
        });
    }

    internal static string CreateToken(BookingChangeAccessTokenEnvelope envelope, string signingKey)
    {
        var payload = JsonSerializer.Serialize(envelope);
        var payloadBase64 = ToBase64Url(Encoding.UTF8.GetBytes(payload));
        var signature = ComputeSignature(payloadBase64, signingKey);
        return $"v1.{payloadBase64}.{signature}";
    }

    private static bool TryParseToken(string token, out BookingChangeAccessTokenEnvelope? envelope, out string? error)
    {
        envelope = null;
        error = null;

        var trimmed = token.Trim();
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["Bearer ".Length..].Trim();

        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "v1", StringComparison.Ordinal))
        {
            error = "Client token format is invalid.";
            return false;
        }

        try
        {
            var payloadBytes = FromBase64Url(parts[1]);
            envelope = JsonSerializer.Deserialize<BookingChangeAccessTokenEnvelope>(payloadBytes);
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.BookingId))
            {
                error = "Client token payload is invalid.";
                return false;
            }

            envelope = envelope with { Signature = parts[2] };
            return true;
        }
        catch
        {
            error = "Client token payload is invalid.";
            return false;
        }
    }

    private static bool ValidateSignature(string token, string signingKey)
    {
        var parts = token.Trim().Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        var expected = ComputeSignature(parts[1], signingKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(parts[2]));
    }

    private static string ComputeSignature(string payloadBase64, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64)));
    }

    private int ResolveExpiryHours(int? requested)
    {
        if (requested is > 0)
            return requested.Value;

        return _options.LinkExpiryHours > 0 ? _options.LinkExpiryHours : 720;
    }

    private string? BuildAccessUrl(string bookingId, string token)
    {
        if (string.IsNullOrWhiteSpace(_options.SelfServiceBaseUrl))
            return null;

        var root = _options.SelfServiceBaseUrl.TrimEnd('/');
        return $"{root}/bookings/{Uri.EscapeDataString(bookingId)}?token={Uri.EscapeDataString(token)}";
    }

    private static string HashToken(string token)
    {
        var trimmed = token.Trim();
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(trimmed)));
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        return Convert.FromBase64String(normalized);
    }
}

public sealed record BookingChangeAccessTokenEnvelope(
    string BookingId,
    string ActorType,
    DateTimeOffset ExpiresUtc,
    string? ActorId = null,
    string? TransactionRef = null,
    string? CorrelationId = null,
    string? Signature = null,
    string? LinkId = null);
