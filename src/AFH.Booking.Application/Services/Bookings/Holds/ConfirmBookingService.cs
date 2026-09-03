using AFH.Booking.Application.Abstractions.Governance;
using AFH.Booking.Application.Abstractions.Clients;
using AFH.Booking.Application.Abstractions.Lifecycle;
using AFH.Booking.Application.Abstractions.Location;
using AFH.Booking.Application.Abstractions.Meetings;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Application.EmailTemplates;
using AFH.Booking.Application.Models.Bookings;
using AFH.Booking.Application.Bookings;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Application.Services.AdviserProjection;
using AFH.Booking.Application.Services.Notifications;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AFH.Booking.Application.Holds;

public sealed class ConfirmBookingService : IConfirmBookingService
{
    private readonly IBookingHoldRepository _holds;
    private readonly IBookingSlotRepository _slots;
    private readonly IBookingTransactionRepository _tx;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICalendarGateway _calendar;
    private readonly IAdviserProfileProjectionRepository _profiles;
    private readonly IMeetingLinkFactory _meetingLinks;
    private readonly IBookingConflictService _conflicts;
    private readonly ISelectedSlotRouteTimeGuard _routeTimeGuard;
    private readonly IBookingLifecycleRecorder _lifecycle;
    private readonly IBookingWorkflowNotificationAdapter _notifications;
    private readonly IHoldWindowFactory _holdWindowFactory;
    private readonly IBookingTokenService _tokenService;
    private readonly NotificationsOptions _notificationOptions;
    private readonly IClientDirectory? _clients;
    private readonly IDownstreamUpdateService? _downstreamUpdates;

    public ConfirmBookingService(
        IBookingHoldRepository holds,
        IBookingSlotRepository slots,
        IBookingTransactionRepository tx,
        IUnitOfWork uow,
        IClock clock,
        ICalendarGateway calendar,
        IAdviserProfileProjectionRepository profiles,
        IMeetingLinkFactory meetingLinks,
        IBookingConflictService conflicts,
        ISelectedSlotRouteTimeGuard routeTimeGuard,
        IBookingLifecycleRecorder lifecycle,
        IBookingWorkflowNotificationAdapter notifications,
        IHoldWindowFactory holdWindowFactory,
        IBookingTokenService tokenService,
        IOptions<NotificationsOptions> notificationOptions,
        IClientDirectory? clients = null,
        IDownstreamUpdateService? downstreamUpdates = null)
    {
        _holds = holds;
        _slots = slots;
        _tx = tx;
        _uow = uow;
        _clock = clock;
        _calendar = calendar;
        _profiles = profiles;
        _meetingLinks = meetingLinks;
        _conflicts = conflicts;
        _routeTimeGuard = routeTimeGuard;
        _lifecycle = lifecycle;
        _notifications = notifications;
        _holdWindowFactory = holdWindowFactory;
        _tokenService = tokenService;
        _notificationOptions = notificationOptions.Value;
        _clients = clients;
        _downstreamUpdates = downstreamUpdates;
    }

    public async Task<Result<ConfirmBookingResponse>> HandleAsync(
        ConfirmBookingCommand cmd,
        CancellationToken ct)
    {
        var utcNow = _clock.UtcNow;

        var contextResult = await LoadConfirmationContextAsync(cmd, utcNow, ct);
        if (!contextResult.IsSuccess || contextResult.Value is null)
            return FailLike<ConfirmationContext, ConfirmBookingResponse>(contextResult);

        var context = contextResult.Value;
        // Confirmation idempotency is state-based: GetForUpdateAsync plus hold status validation
        // prevents duplicate side effects. The workflow key is recorded for audit/notification lookup,
        // not used as a lock.
        var workflowKey = BookingWorkflowIdempotencyKeys.Confirmation(context.Hold.Id);

        var routeTimeResult = await ApplyRouteTimeSnapshotIfRequiredAsync(context, utcNow, ct);
        if (!routeTimeResult.IsSuccess)
            return FailLike<ConfirmBookingResponse>(routeTimeResult);

        var calendarUserIdResult = await ResolveCalendarUserAndCheckConflictsAsync(context, ct);
        if (!calendarUserIdResult.IsSuccess || calendarUserIdResult.Value is null)
            return FailLike<string, ConfirmBookingResponse>(calendarUserIdResult);

        var before = CreateSnapshot(context.Hold, context.Slot, context.Transaction);
        await ConfirmHoldAndTransactionAsync(context, utcNow, ct);

        var joinUrl = await CreateJoinLinkIfRemoteAsync(context, ct);
        var selfServiceLinks = await BuildSelfServiceLinksAsync(context.Hold.Id, ct);
        await UpdateConfirmedCalendarEventAsync(context, calendarUserIdResult.Value, joinUrl, selfServiceLinks, ct);

        var eventId = await RecordBookedLifecycleAsync(cmd, context, before, workflowKey, utcNow, ct);
        await _uow.SaveChangesAsync(ct);

        await SendBookedNotificationAsync(context, eventId, joinUrl, selfServiceLinks, ct);
        await _uow.SaveChangesAsync(ct);

        await PublishBookedUpdateAsync(context, eventId, ct);

        return OkResponse(context.Hold, context.Transaction, joinUrl);
    }

    private async Task<Result<ConfirmationContext>> LoadConfirmationContextAsync(
        ConfirmBookingCommand cmd,
        DateTime utcNow,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.HoldId))
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.BadRequest,
                "holdId is required.",
                Errors.Validation);
        }

        var hold = await _holds.GetForUpdateAsync(cmd.HoldId.Trim(), ct);
        if (hold is null)
            return Result<ConfirmationContext>.NotFound($"Hold '{cmd.HoldId}' not found.");

        var holdStatusResult = ValidateHoldCanBeConfirmed(hold, utcNow);
        if (!holdStatusResult.IsSuccess)
            return FailLike<ConfirmationContext>(holdStatusResult);

        var slot = await _slots.GetAsync(hold.SlotId, ct);
        if (slot is null)
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.Conflict,
                $"Slot '{hold.SlotId}' not found.",
                Errors.HoldSlotMissing);
        }

        if (slot.StartUtc <= utcNow)
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.Conflict,
                "The booking cannot be confirmed because the meeting start time has passed.",
                Errors.SlotNoLongerAvailable);
        }

        var tx = await _tx.GetForUpdateAsync(slot.TransactionId, ct);
        if (tx is null)
        {
            return Result<ConfirmationContext>.Fail(
                HttpStatusCode.Conflict,
                $"Transaction '{slot.TransactionId}' not found.",
                Errors.HoldTransactionMissing);
        }

        return Result<ConfirmationContext>.Ok(new ConfirmationContext(hold, slot, tx)
        {
            CommandActorContext = cmd.ActorContext
        });
    }

    private static Result ValidateHoldCanBeConfirmed(BookingHold hold, DateTime utcNow)
    {
        if (hold.Status == BookingHoldStatus.Cancelled)
            return Result.Fail(HttpStatusCode.Conflict, "Hold already cancelled.", Errors.HoldCancelled);

        if (hold.Status == BookingHoldStatus.Confirmed)
            return Result.Fail(HttpStatusCode.Conflict, "Hold already confirmed.", Errors.HoldAlreadyConfirmed);

        if (hold.ExpiresUtc <= utcNow)
            return Result.Fail(HttpStatusCode.Conflict, "Hold has expired.", Errors.HoldExpired);

        if (string.IsNullOrWhiteSpace(hold.SlotId))
            return Result.Fail(HttpStatusCode.Conflict, "Hold has no slotId.", Errors.HoldStateInvalid);

        return Result.Ok();
    }

    private async Task<Result> ApplyRouteTimeSnapshotIfRequiredAsync(
        ConfirmationContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        var routeTimeCheck = await _routeTimeGuard.EvaluateAsync(
            context.Slot,
            context.Transaction,
            context.Hold.Id,
            ct);

        if (!routeTimeCheck.IsAllowed)
        {
            return Result.Fail(
                HttpStatusCode.Conflict,
                routeTimeCheck.ErrorMessage ?? "The selected slot is no longer available.",
                routeTimeCheck.ErrorCode ?? Errors.ExactRouteTimeUnavailable);
        }

        if (!routeTimeCheck.WasTriggered ||
            !routeTimeCheck.TravelTimeMinutes.HasValue ||
            !routeTimeCheck.TravelDistanceMiles.HasValue)
        {
            return Result.Ok();
        }

        context.Slot.AttachTravelSnapshot(
            travelMinutes: routeTimeCheck.TravelTimeMinutes,
            distanceMiles: routeTimeCheck.TravelDistanceMiles,
            companyBufferMinutes: context.Slot.CompanyBufferMinutes,
            sourceLocationRef: context.Slot.SourceLocationRef,
            sourcePostcode: context.Slot.SourcePostcode,
            sourceLatitude: context.Slot.SourceLatitude,
            sourceLongitude: context.Slot.SourceLongitude,
            destinationLocationRef: context.Slot.DestinationLocationRef,
            destinationPostcode: context.Slot.DestinationPostcode,
            destinationLatitude: context.Slot.DestinationLatitude,
            destinationLongitude: context.Slot.DestinationLongitude,
            provider: "LocationRouteTime",
            confidence: "Exact",
            calculatedUtc: utcNow);

        await _slots.UpdateAsync(context.Slot, ct);
        return Result.Ok();
    }

    private async Task<Result<string>> ResolveCalendarUserAndCheckConflictsAsync(
        ConfirmationContext context,
        CancellationToken ct)
    {
        var calendarUserId = await _profiles.ResolveCalendarUserIdAsync(context.Slot.AdviserId, ct);

        var conflicts = await _conflicts.EvaluateConfirmationConflictsAsync(
            context.Hold,
            context.Slot,
            context.Transaction,
            calendarUserId,
            ct);

        if (conflicts.IsBlocked)
        {
            return Result<string>.Fail(
                HttpStatusCode.Conflict,
                conflicts.ErrorMessage ?? "Booking confirmation blocked by calendar conflict.",
                conflicts.ErrorCode ?? Errors.Conflict);
        }

        return Result<string>.Ok(calendarUserId);
    }

    private async Task ConfirmHoldAndTransactionAsync(
        ConfirmationContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        context.Hold.Confirm(utcNow);
        await _holds.UpdateAsync(context.Hold, ct);

        if (context.Transaction.Status != BookingTransactionStatus.Open)
            return;

        context.Transaction.MarkCompleted();
        await _tx.UpdateAsync(context.Transaction, ct);
    }

    private async Task<string?> CreateJoinLinkIfRemoteAsync(
        ConfirmationContext context,
        CancellationToken ct)
    {
        return context.Transaction.IsRemote
            ? await _meetingLinks.CreateJoinLinkAsync(context.Hold.Id, ct)
            : null;
    }

    private async Task UpdateConfirmedCalendarEventAsync(
        ConfirmationContext context,
        string calendarUserId,
        string? joinUrl,
        BookingSelfServiceLinks? selfServiceLinks,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Hold.CalendarProviderEventId))
            return;

        var windows = _holdWindowFactory.Create(context.Slot, context.Transaction);
        var calendarTemplate = ConfirmedBookingTemplate.BuildConfirmedTemplate(
            slot: context.Slot,
            tx: context.Transaction,
            booking: context.Hold,
            windows: windows,
            joinUrl: joinUrl,
            location: null,
            selfServiceLinks: selfServiceLinks);

        var calendarEvent = BookingCalendarEvent.Update(
            userId: calendarUserId,
            providerEventId: context.Hold.CalendarProviderEventId,
            showAs: BookingShowAs.Busy,
            body: calendarTemplate.CalendarDescription,
            categories: new[] { "AFH Booking", "Confirmed" });

        await _calendar.UpdateBookingEventAsync(calendarEvent, ct);
    }

    private async Task<string> RecordBookedLifecycleAsync(
        ConfirmBookingCommand cmd,
        ConfirmationContext context,
        object before,
        string workflowKey,
        DateTime utcNow,
        CancellationToken ct)
    {
        var eventId = await _lifecycle.RecordEventAsync(
            new BookingLifecycleEventRecord(
                BookingId: context.Hold.Id,
                TransactionId: context.Transaction.Id,
                EventType: LifecycleEventTypes.Booked,
                ActorContext: cmd.ActorContext,
                ActorType: LifecycleActors.Client,
                ActorId: null,
                ReasonCode: null,
                ReasonNotes: cmd.Notes,
                Before: before,
                After: CreateSnapshot(context.Hold, context.Slot, context.Transaction),
                OccurredUtc: utcNow,
                CorrelationId: null,
                SourceSystem: "BookingService",
                RelatedBookingId: null,
                PreviousState: null,
                NewState: LifecycleStates.Booked,
                TriggerReason: workflowKey),
            ct);

        await _lifecycle.RecordStepAsync(
            eventId,
            new BookingLifecycleStepRecord(
                LifecycleStepNames.Outlook,
                1,
                string.IsNullOrWhiteSpace(context.Hold.CalendarProviderEventId)
                    ? LifecycleStepStatuses.Skipped
                    : LifecycleStepStatuses.Succeeded,
                utcNow,
                _clock.UtcNow,
                ActorContext: cmd.ActorContext),
            ct);

        await _lifecycle.RecordStepAsync(
            eventId,
            new BookingLifecycleStepRecord(
                LifecycleStepNames.SqlAudit,
                2,
                LifecycleStepStatuses.Succeeded,
                utcNow,
                _clock.UtcNow,
                ActorContext: cmd.ActorContext),
            ct);

        return eventId;
    }

    private async Task SendBookedNotificationAsync(
        ConfirmationContext context,
        string eventId,
        string? joinUrl,
        BookingSelfServiceLinks? links,
        CancellationToken ct)
    {
        var notificationStartedUtc = _clock.UtcNow;
        var notificationStatus = LifecycleStepStatuses.Succeeded;
        string? notificationErrorCode = null;
        string? notificationErrorDetails = null;
        BookingWorkflowNotificationOutcome? outcome = null;

        try
        {
            var client = _clients is null
                ? null
                : await _clients.GetAsync(context.Transaction.TransactionRef, ct);

            outcome = await _notifications.RequestAsync(
                new BookingWorkflowNotificationRequest(
                    LifecycleEventTypes.Booked,
                    context.Hold.Id,
                    ResolveNotificationActorType(context),
                    BuildBookingConfirmedRecipients(client),
                    BuildBookingConfirmedNotificationData(
                        context,
                        _holdWindowFactory.Create(context.Slot, context.Transaction),
                        eventId,
                        joinUrl,
                        links,
                        client)),
                ct);
        }
        catch (Exception)
        {
            outcome = BookingWorkflowNotificationOutcome.Failed(
                "BookingConfirmed",
                0,
                failureCode: LifecycleErrorCodes.NotificationFailed);
        }

        if (outcome is not null)
        {
            notificationStatus = outcome.ToLifecycleStepStatus();
            notificationErrorCode = outcome.ToLifecycleStepErrorCode();
            notificationErrorDetails = outcome.ToLifecycleStepDetails();
        }

        await _lifecycle.RecordStepAsync(
            eventId,
            new BookingLifecycleStepRecord(
                LifecycleStepNames.Notifications,
                3,
                notificationStatus,
                notificationStartedUtc,
                _clock.UtcNow,
                notificationErrorCode,
                notificationErrorDetails,
                ActorContext: context.CommandActorContext),
            ct);
    }

    private static string ResolveNotificationActorType(ConfirmationContext context)
        => string.IsNullOrWhiteSpace(context.CommandActorContext?.ActorType)
            ? LifecycleActors.Client
            : context.CommandActorContext.ActorType;

    private async Task<BookingSelfServiceLinks?> BuildSelfServiceLinksAsync(string bookingId, CancellationToken ct)
    {
        var tokenResult = await _tokenService.GenerateClientAccessTokenAsync(bookingId, ct);
        return tokenResult.IsSuccess
            ? BookingSelfServiceLinkBuilder.Build(_notificationOptions.ClientPortalBaseUrl, bookingId, tokenResult.Value)
            : null;
    }

    private async Task PublishBookedUpdateAsync(
        ConfirmationContext context,
        string eventId,
        CancellationToken ct)
    {
        if (_downstreamUpdates is null)
            return;

        await _downstreamUpdates.PublishBookingChangeAsync(
            bookingId: context.Hold.Id,
            changeType: "Booked",
            transactionRef: context.Transaction.TransactionRef,
            payloadJson: JsonSerializer.Serialize(new
            {
                bookingId = context.Hold.Id,
                transactionRef = context.Transaction.TransactionRef,
                performedBy = DownstreamPerformedByResolver.Resolve(
                    context.CommandActorContext,
                    BookingActorContext.ActorClient),
                bookingReference = context.Transaction.BookingReference ?? context.Hold.Reference ?? context.Transaction.TransactionRef,
                slotId = context.Slot.Id,
                adviserId = context.Slot.AdviserId,
                startUtc = context.Slot.StartUtc,
                endUtc = context.Slot.EndUtc,
                meetingType = context.Transaction.MeetingType,
                meetingMode = context.Transaction.IsRemote ? "online" : "face-to-face",
                lifecycleEventId = eventId
            }),
            ct: ct);
    }

    private static Result<ConfirmBookingResponse> OkResponse(
        BookingHold hold,
        BookingTransaction tx,
        string? joinUrl = null)
    {
        return Result<ConfirmBookingResponse>.Ok(
            new ConfirmBookingResponse
            {
                BookingId = hold.Id,
                BookingReference = tx.BookingReference ?? hold.Reference,
                SlotId = hold.SlotId,
                TransactionId = tx.Id,
                TransactionRef = tx.TransactionRef,
                Status = BookingHoldStatus.Confirmed.ToString(),
                LifecycleState = LifecycleEventTypes.Booked,
                OnlineMeetingJoinUrl = joinUrl
            });
    }

    private static object CreateSnapshot(
        BookingHold hold,
        BookingSlot slot,
        BookingTransaction tx)
    {
        return new
        {
            bookingId = hold.Id,
            lifecycleState = hold.Status == BookingHoldStatus.Confirmed
                ? LifecycleEventTypes.Booked
                : hold.Status.ToString(),
            holdStatus = hold.Status.ToString(),
            holdConfirmedUtc = hold.ConfirmedUtc,
            slotId = slot.Id,
            slotStartUtc = slot.StartUtc,
            slotEndUtc = slot.EndUtc,
            adviserId = slot.AdviserId,
            transactionId = tx.Id,
            transactionRef = tx.TransactionRef,
            transactionStatus = tx.Status.ToString()
        };
    }

    private static IReadOnlyList<BookingNotificationRecipient> BuildBookingConfirmedRecipients(
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

    private static IReadOnlyDictionary<string, string> BuildBookingConfirmedNotificationData(
        ConfirmationContext context,
        HoldWindows windows,
        string eventId,
        string? joinUrl,
        BookingSelfServiceLinks? links,
        Domain.Client.ClientDirectoryItem? client)
    {
        var text = ConfirmedBookingTemplate.BuildConfirmedTemplate(
            context.Slot,
            context.Transaction,
            context.Hold,
            windows,
            joinUrl,
            location: null,
            links);
        var lines = text.TextBody.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var dataByPrefix = lines
            .Select(line => line.Split(": ", 2, StringSplitOptions.None))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var data = new Dictionary<string, string>
        {
            ["eventId"] = eventId,
            ["slotId"] = context.Slot.Id,
            ["adviserId"] = context.Slot.AdviserId,
            ["adviserName"] = context.Slot.AdviserName,
            ["startUtc"] = context.Slot.StartUtc.ToString("O"),
            ["endUtc"] = context.Slot.EndUtc.ToString("O"),
            ["transactionRef"] = context.Transaction.TransactionRef,
            ["bookingId"] = context.Hold.Id,
            ["IdempotencyKey"] = BookingWorkflowIdempotencyKeys.Notification("booking-confirmed", context.Hold.Id),
            ["joinUrl"] = joinUrl ?? string.Empty,
            ["meetingType"] = dataByPrefix.GetValueOrDefault("Meeting type", "N/A"),
            ["when"] = dataByPrefix.GetValueOrDefault("When", string.Empty),
            ["whereLine"] = lines.FirstOrDefault(line =>
                line.StartsWith("Join link:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Location:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty,
            ["travelLine"] = lines.FirstOrDefault(line =>
                line.StartsWith("Travel:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Travel time:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Travel buffer:", StringComparison.OrdinalIgnoreCase)) ?? string.Empty,
            ["manageBookingLinks"] = BuildConfirmedManageLinks(links)
        };

        if (links is not null)
        {
            data["viewBookingUrl"] = links.ViewBookingUrl;
            data["cancelBookingUrl"] = links.CancelBookingUrl;
            data["rescheduleBookingUrl"] = links.RescheduleBookingUrl;
        }

        BookingNotificationPayloadFields.AddStandardBookingFields(
            data,
            context.Transaction,
            context.Slot,
            "Confirmed",
            joinUrl,
            links?.ViewBookingUrl);

        AddClientAndMeetingLocation(data, context.Transaction, client);
        return data;
    }

    private static void AddClientAndMeetingLocation(
        Dictionary<string, string> data,
        BookingTransaction transaction,
        Domain.Client.ClientDirectoryItem? client)
    {
        data["clientName"] = FirstNonEmpty(
            transaction.ClientName,
            BuildClientDisplayName(client));
        data["clientEmail"] = FirstNonEmpty(transaction.ClientEmail, client?.Email);
        data["clientPhone"] = client?.Phone?.Trim() ?? string.Empty;
        data["meetingAddressLine1"] = FirstNonEmpty(transaction.ClientAddressLine1, client?.StreetName1);
        data["meetingAddressLine2"] = FirstNonEmpty(transaction.ClientAddressLine2, client?.StreetName2);
        data["meetingTown"] = FirstNonEmpty(transaction.ClientTown, client?.Town);
        data["meetingCounty"] = FirstNonEmpty(transaction.ClientCounty, client?.County);
        data["meetingPostcode"] = FirstNonEmpty(transaction.ClientPostcode, client?.PostalCode);
        data["meetingAddress"] = BuildMeetingAddress(data);
    }

    private static string BuildClientDisplayName(Domain.Client.ClientDirectoryItem? client)
        => client is null
            ? string.Empty
            : FirstNonEmpty($"{client.FirstName} {client.LastName}".Trim(), client.Email);

    private static string BuildMeetingAddress(Dictionary<string, string> data)
    {
        string Get(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;

        return string.Join(", ", new[]
        {
            Get("meetingAddressLine1"),
            Get("meetingAddressLine2"),
            Get("meetingTown"),
            Get("meetingCounty"),
            Get("meetingPostcode")
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string BuildConfirmedManageLinks(BookingSelfServiceLinks? links)
    {
        if (links is null)
            return string.Empty;

        return
$@"
Manage your booking:
- View booking: {links.ViewBookingUrl}
- Cancel booking: {links.CancelBookingUrl}
- Reschedule booking: {links.RescheduleBookingUrl}";
    }

    private static Result<T> FailLike<T>(Result failure)
    {
        return Result<T>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private static Result<TTo> FailLike<TFrom, TTo>(Result<TFrom> failure)
    {
        return Result<TTo>.Fail(
            failure.StatusCode,
            failure.ErrorMessage ?? "Request failed.",
            failure.ErrorCode);
    }

    private sealed record ConfirmationContext(
        BookingHold Hold,
        BookingSlot Slot,
        BookingTransaction Transaction)
    {
        public BookingActorContext? CommandActorContext { get; init; }
    }
}
