using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class ProcessNotificationsHandler : IProcessNotificationsHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private const int RawPayloadMaxLen = 4000;

    private readonly ILogger<ProcessNotificationsHandler> _logger;
    private readonly CalendarSubscriptionOptions _opts;
    private readonly ICalendarNotificationRepository _notifications;
    private readonly ICalendarSubscriptionRepository _subscriptions;
    private readonly ICalendarGateway _calendar;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICalendarEventSnapshotRepository _snapshots;
    private readonly IAdviserAvailabilityProjectionRepository _availabilityProjection;
    private readonly CalendarProjectionOptions _projectionOptions;


    public ProcessNotificationsHandler(
       ILogger<ProcessNotificationsHandler> logger,
       IOptions<CalendarSubscriptionOptions> opts,
       ICalendarNotificationRepository receipts,
       ICalendarSubscriptionRepository subscriptions,
       ICalendarEventSnapshotRepository snapshots,
       ICalendarGateway calendar,
       IUnitOfWork uow,
       IClock clock,
       IAdviserAvailabilityProjectionRepository availabilityProjection,
       IOptions<CalendarProjectionOptions> projectionOptions)
    {
        _logger = logger;
        _opts = opts.Value;
        _notifications = receipts;
        _subscriptions = subscriptions;
        _snapshots = snapshots;
        _calendar = calendar;
        _uow = uow;
        _clock = clock;
        _availabilityProjection = availabilityProjection;
        _projectionOptions = projectionOptions.Value;
    }

    public async Task<Result> HandleAsync(CalendarNotificationsRequest? envelope, CancellationToken ct)
    {
        var items = envelope?.Value ?? [];

        if (items.Count == 0)
        {
            _logger.LogInformation("Calendar notifications received with empty payload.");
            return Result.Ok();
        }

        foreach (var n in items)
        {
            if (ct.IsCancellationRequested)
                break;

            var subscriptionId = n.SubscriptionId?.Trim();
            var clientState = n.ClientState?.Trim();
            var eventId = n.ResourceData?.Id?.Trim();
            var changeType = n.ChangeType?.Trim();

            var accepted = true;
            string? rejectReason = null;

            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(eventId))
            {
                accepted = false;
                rejectReason = "Missing SubscriptionId/EventId";
            }
            else if (_opts.RequireClientState &&
                     !string.Equals(clientState, _opts.ClientState, StringComparison.Ordinal))
            {
                accepted = false;
                rejectReason = "Invalid ClientState";
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId) && !string.IsNullOrWhiteSpace(eventId))
            {
                var sinceUtc = _clock.UtcNow.AddMinutes(-Math.Max(1, _projectionOptions.DedupeWindowMinutes));
                var duplicate = await _notifications.ExistsRecentDuplicateAsync(
                    subscriptionId,
                    eventId,
                    changeType,
                    sinceUtc,
                    ct);

                if (duplicate)
                {
                    _logger.LogInformation(
                        "Skipping duplicate calendar notification. SubscriptionId={SubscriptionId} EventId={EventId} ChangeType={ChangeType}",
                        subscriptionId,
                        eventId,
                        changeType);
                    continue;
                }
            }

            // raw payload (store the notification as JSON, truncate to column limit)
            string? rawPayload = null;
            try
            {
                rawPayload = JsonSerializer.Serialize(n, JsonOpts);
                if (rawPayload.Length > RawPayloadMaxLen)
                    rawPayload = rawPayload[..RawPayloadMaxLen];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialise raw calendar notification payload.");
            }



            var receipt = CalendarNotificationReceipt.Create(
                subscriptionId: subscriptionId!,
                eventId: eventId!,
                changeType: changeType,
                clientState: clientState,
                accepted: accepted,
                rejectReason: rejectReason,
                receivedUtc: _clock.UtcNow,
                rawPayload: rawPayload);

            await _notifications.AddAsync(receipt, ct);





            // Keep projection in sync for delete events even when no snapshot fetch is possible.
            if (accepted && n.ChangeType is "deleted")
            {
                var sub = await _subscriptions.GetBySubscriptionIdAsync(subscriptionId!, ct);
                if (sub is not null)
                {
                    await _availabilityProjection.DeleteBusyBlockAsync(
                        sub.UserId,
                        eventId!,
                        _clock.UtcNow,
                        ct);
                }
            }

            // 2) Only attempt snapshot fetch for accepted created/updated
            if (accepted && (n.ChangeType is "created" or "updated"))
            {
                var sub = await _subscriptions.GetBySubscriptionIdAsync(subscriptionId!, ct);

                if (sub is null)
                {
                    _logger.LogWarning("No subscription found in DB for SubscriptionId={SubscriptionId}. Cannot fetch snapshot.", subscriptionId);
                }
                else
                {
                    try
                    {
                        // whatever your gateway returns - you need these fields
                        var evt = await _calendar.GetEventAsync(sub.UserId, eventId!, ct);
                        if (evt is null)
                            throw new InvalidOperationException("Calendar event lookup returned null.");

                        var snap = CalendarEventSnapshot.CreateSuccess(
                            id: Guid.NewGuid().ToString("N"),
                            receiptId: receipt.Id,
                            userId: sub.UserId,
                            providerEventId: eventId!,
                            calendarId: evt.CalendarId,
                            subject: evt.Subject,
                            startUtc: evt.StartUtc,
                            endUtc: evt.EndUtc,
                            isCancelled: n.ChangeType == "deleted",
                            changeKey: evt.ChangeKey,
                            iCalUId: evt.ICalUId,
                            fetchedUtc: _clock.UtcNow
                        );

                        await _snapshots.AddAsync(snap, ct); // or UpsertAsync

                        if (evt.StartUtc < evt.EndUtc)
                        {
                            await _availabilityProjection.UpsertBusyBlockAsync(
                                new AdviserBusyBlockProjection
                                {
                                    Id = Guid.NewGuid().ToString("N"),
                                    AdviserId = sub.UserId,
                                    ProviderEventId = eventId!,
                                    CalendarId = evt.CalendarId,
                                    Subject = evt.Subject,
                                    StartUtc = DateTime.SpecifyKind(evt.StartUtc, DateTimeKind.Utc),
                                    EndUtc = DateTime.SpecifyKind(evt.EndUtc, DateTimeKind.Utc),
                                    IsCancelled = false,
                                    ChangeKey = evt.ChangeKey,
                                    ICalUId = evt.ICalUId,
                                    LastSyncedUtc = _clock.UtcNow,
                                    SourceReceiptId = receipt.Id
                                },
                                ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch event snapshot. SubscriptionId={SubscriptionId} EventId={EventId}", subscriptionId, eventId);

                        // Optional: store a failure snapshot too, still linked to the receipt
                        var failSnap = CalendarEventSnapshot.CreateFailure(
                            id: Guid.NewGuid().ToString("N"),
                            receiptId: receipt.Id,
                            userId: sub.UserId,
                            providerEventId: eventId!,
                            fetchedUtc: _clock.UtcNow,
                            fetchError: ex.Message
                        );

                        await _snapshots.AddAsync(failSnap, ct);
                    }
                }
            }

            if (accepted)
                _logger.LogInformation(
                    "Calendar notification accepted. SubscriptionId={SubscriptionId} EventId={EventId} ChangeType={ChangeType}",
                    subscriptionId, eventId, changeType);
            else
                _logger.LogWarning(
                    "Calendar notification rejected. SubscriptionId={SubscriptionId} EventId={EventId} Reason={Reason}",
                    subscriptionId, eventId, rejectReason);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
