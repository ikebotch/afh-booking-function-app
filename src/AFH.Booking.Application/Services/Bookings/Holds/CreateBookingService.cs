

using AFH.Booking.Application.Abstractions.Bookings.Holds;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Models.Lifecycle.Constants;
using AFH.Booking.Domain.Bookings.Commands;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Holds;

public sealed class CreateBookingService : ICreateBookingService
{
    private readonly IBookingContextLoader _loader;
    private readonly IBookingHoldService _holdService;
    private readonly IBookingCalendarService _calendarService;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IBookingLifecycleRecorder _lifecycle;
    private readonly IBookingWorkflowNotificationAdapter _notifications;
    private readonly IClientDirectory? _clients;
    private readonly ILogger<CreateBookingService> _logger;

    public CreateBookingService(
        IBookingContextLoader loader,
        IBookingHoldService holdService,
        IBookingCalendarService calendarService,
        IUnitOfWork uow,
        IClock clock,
        IBookingLifecycleRecorder lifecycle,
        IBookingWorkflowNotificationAdapter notifications,
        ILogger<CreateBookingService> logger,
        IClientDirectory? clients = null)
    {
        _loader = loader;
        _holdService = holdService;
        _calendarService = calendarService;
        _uow = uow;
        _clock = clock;
        _lifecycle = lifecycle;
        _notifications = notifications;
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

        var lifecycleEventId = await RecordHoldCreatedEventAsync(cmd, hold, context, utcNow, ct);

        await _uow.SaveChangesAsync(ct);

        await PublishHoldCreatedNotificationAsync(lifecycleEventId, cmd, hold, context, ct);

        await _uow.SaveChangesAsync(ct);

        return Result<CreateBookingResponse>.Ok(CreateResponse(
            hold,
            context.Transaction,
            context.Slot));
    }

    private async Task<string> RecordHoldCreatedEventAsync(
        CreateHoldCommand cmd,
        BookingHold hold,
        BookingContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        var eventId = await _lifecycle.RecordEventAsync(new BookingLifecycleEventRecord(
            BookingId: hold.Id,
            TransactionId: context.Transaction.TransactionRef,
            EventType: LifecycleEventTypes.HoldCreated,
            ActorContext: cmd.ActorContext,
            ActorType: LifecycleActors.System,
            ActorId: null,
            ReasonCode: null,
            ReasonNotes: null,
            Before: null,
            After: new
            {
                hold.Id,
                hold.SlotId,
                context.Slot.AdviserId,
                hold.ExpiresUtc,
                context.Transaction.TransactionRef,
                context.Slot.StartUtc,
                context.Slot.EndUtc
            },
            OccurredUtc: utcNow,
            CorrelationId: null,
            SourceSystem: "BookingService",
            PreviousState: null,
            NewState: null,
            TriggerReason: "CreateHold"), ct);

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Outlook,
            1,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            _clock.UtcNow,
            ActorContext: cmd.ActorContext), ct);

        await _lifecycle.RecordStepAsync(eventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.SqlAudit,
            2,
            LifecycleStepStatuses.Succeeded,
            utcNow,
            _clock.UtcNow,
            ActorContext: cmd.ActorContext), ct);

        return eventId;
    }

    private async Task PublishHoldCreatedNotificationAsync(
        string lifecycleEventId,
        CreateHoldCommand cmd,
        BookingHold hold,
        BookingContext context,
        CancellationToken ct)
    {
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;
        var startedUtc = _clock.UtcNow;
        BookingWorkflowNotificationOutcome? outcome = null;

        try
        {
            var client = _clients is null
                ? null
                : await _clients.GetAsync(context.Transaction.TransactionRef, ct);

            outcome = await _notifications.RequestAsync(
                new BookingWorkflowNotificationRequest(
                    LifecycleEventTypes.HoldCreated,
                    hold.Id,
                    ResolveNotificationActorType(cmd),
                    BuildHoldCreatedRecipients(client),
                    BuildHoldCreatedNotificationData(hold, context)),
                ct);
        }
        catch (Exception ex)
        {
            outcome = BookingWorkflowNotificationOutcome.Failed(
                "BookingHoldCreated",
                0,
                failureCode: LifecycleErrorCodes.NotificationFailed);
            _logger.LogWarning(ex, "Hold notification publish failed for HoldId={HoldId}. Hold creation succeeded.", hold.Id);
        }

        if (outcome is not null)
        {
            notificationStatus = outcome.ToLifecycleStepStatus();
            notificationErrorCode = outcome.ToLifecycleStepErrorCode();
            notificationErrorDetails = outcome.ToLifecycleStepDetails();
        }

        await _lifecycle.RecordStepAsync(lifecycleEventId, new BookingLifecycleStepRecord(
            LifecycleStepNames.Notifications,
            3,
            notificationStatus,
            startedUtc,
            _clock.UtcNow,
            notificationErrorCode,
            notificationErrorDetails,
            ActorContext: cmd.ActorContext), ct);
    }

    private static string ResolveNotificationActorType(CreateHoldCommand cmd)
        => string.IsNullOrWhiteSpace(cmd.ActorContext?.ActorType)
            ? LifecycleActors.System
            : cmd.ActorContext.ActorType;

    private static IReadOnlyList<BookingNotificationRecipient> BuildHoldCreatedRecipients(
        Domain.Client.ClientDirectoryItem? client)
    {
        if (client is null)
            return Array.Empty<BookingNotificationRecipient>();

        var displayName = $"{client.FirstName} {client.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = null;

        return
        [
            new BookingNotificationRecipient(
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
            ["IdempotencyKey"] = BookingWorkflowIdempotencyKeys.Notification("booking-hold-created", hold.Id),
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
            BookingReference = hold.Reference,
            SlotId = hold.SlotId,
            HoldExpiresUtc = hold.ExpiresUtc,
            CompanyBufferMinutes = tx.IsRemote
                ? 0
                : Math.Max(0, slot.CompanyBufferMinutes ?? 30)
        };
    }
}
