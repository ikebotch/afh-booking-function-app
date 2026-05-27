

using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Notification.Contract.Abstractions;
using AFH.Notification.Contract.V1.Dtos;
using AFH.Notification.Contract.V1.Requests;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Holds;

public sealed class CreateBookingService : ICreateBookingService
{
    private readonly IBookingContextLoader _loader;
    private readonly IBookingHoldService _holdService;
    private readonly IBookingCalendarService _calendarService;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IClientDirectory? _clients;
    private readonly ILogger<CreateBookingService> _logger;

    public CreateBookingService(
        IBookingContextLoader loader,
        IBookingHoldService holdService,
        IBookingCalendarService calendarService,
        IUnitOfWork uow,
        IClock clock,
        INotificationPublisher notificationPublisher,
        ILogger<CreateBookingService> logger,
        IClientDirectory? clients = null)
    {
        _loader = loader;
        _holdService = holdService;
        _calendarService = calendarService;
        _uow = uow;
        _clock = clock;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _clients = clients;
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

        await PublishHoldCreatedNotificationAsync(hold, context, ct);

        return Result<CreateBookingResponse>.Ok(CreateResponse(
            hold,
            context.Transaction,
            context.Slot));
    }

    private async Task PublishHoldCreatedNotificationAsync(
        BookingHold hold,
        BookingContext context,
        CancellationToken ct)
    {
        try
        {
            var client = _clients is null
                ? null
                : await _clients.GetAsync(context.Transaction.TransactionRef, ct);

            await _notificationPublisher.PublishAsync(
                new NotificationRequested(
                    BookingNotificationTypes.BookingHoldCreated,
                    hold.Id,
                    new NotificationActor(
                        BookingNotificationActorTypes.System,
                        "Booking",
                        null,
                        null,
                        null),
                    BuildHoldCreatedRecipients(client),
                    BuildHoldCreatedNotificationData(hold, context)),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hold notification publish failed for HoldId={HoldId}. Hold creation succeeded.", hold.Id);
        }
    }

    private static IReadOnlyList<NotificationRecipient> BuildHoldCreatedRecipients(
        Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null)
            return Array.Empty<NotificationRecipient>();

        var displayName = $"{client.FirstName} {client.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = null;

        return
        [
            new NotificationRecipient(
                BookingNotificationRecipientTypes.Client,
                displayName,
                client.Email,
                client.Phone)
        ];
    }

    private static IReadOnlyDictionary<string, string> BuildHoldCreatedNotificationData(
        BookingHold hold,
        BookingContext context)
    {
        var tz = string.IsNullOrWhiteSpace(context.Transaction.Timezone)
            ? "UTC"
            : context.Transaction.Timezone.Trim();

        string FormatLocal(DateTime utc)
        {
            try
            {
                if (tz.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                    return utc.ToUniversalTime().ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tz);
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tzInfo);
                return local.ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + $" ({tz})";
            }
            catch
            {
                return utc.ToUniversalTime().ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";
            }
        }

        var startFormatted = FormatLocal(context.Slot.StartUtc);
        var endFormatted = FormatLocal(context.Slot.EndUtc);
        var whenLine = $"{startFormatted} to {endFormatted}";

        var meetingType = context.Transaction.IsRemote ? "Remote meeting" : "In-person meeting";

        var travelLine = context.Transaction.IsRemote
            ? "Travel: N/A (remote meeting)"
            : string.Empty;

        var companyBuffer = context.Transaction.IsRemote
            ? string.Empty
            : $"Company buffer: {Math.Max(0, context.Slot.CompanyBufferMinutes ?? 30)} minutes";

        return new Dictionary<string, string>
        {
            ["transactionRef"] = context.Transaction.TransactionRef,
            ["holdId"] = hold.Id,
            ["adviserName"] = context.Slot.AdviserName,
            ["meetingType"] = meetingType,
            ["when"] = whenLine,
            ["holdExpires"] = FormatLocal(hold.ExpiresUtc),
            ["travelLine"] = travelLine,
            ["companyLine"] = companyBuffer,
            ["manageBookingLinks"] = string.Empty
        };
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
