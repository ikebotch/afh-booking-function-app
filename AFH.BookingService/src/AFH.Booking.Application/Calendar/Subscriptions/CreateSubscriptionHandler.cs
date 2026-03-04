using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Common;
using AFH.Booking.Application.Common.Clock;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Options;
using Common.Utilities;
using Microsoft.Extensions.Options;
using System.Net;

namespace AFH.Booking.Application.Calendar.Subscriptions;

public sealed class CreateSubscriptionHandler : ICreateSubscriptionHandler
{
    private readonly ICalendarSubscriptionGateway _gateway;
    private readonly ICalendarSubscriptionRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly CalendarSubscriptionOptions _opts;

    public CreateSubscriptionHandler(
        ICalendarSubscriptionGateway gateway,
        ICalendarSubscriptionRepository repo,
        IUnitOfWork uow,
        IClock clock,
       IOptions<CalendarSubscriptionOptions> opts)
    {
        _gateway = gateway;
        _repo = repo;
        _uow = uow;
        _clock = clock;
        _opts = opts.Value;
    }

    public async Task<Result<CreateCalendarSubscriptionResult>> HandleAsync(
        CreateCalendarSubscriptionRequest cmd,
        CancellationToken ct)
    {
        if (cmd is null)
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "Request body is required.",
                Errors.Validation);

        if (string.IsNullOrWhiteSpace(cmd.UserId))
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "UserId is required.",
                Errors.Validation);

        var utcNow = _clock.UtcNow;


        var code = Uri.EscapeDataString(_opts.FunctionKey.Trim());


        var placeholders = new Dictionary<string, string>
                {
                    { "baseUrl", _opts.BaseUrl },
                    { "functionKey", code },

                };

        string genNotificationUrl = UrlTemplateHelper.Build(_opts.NotificationsUrl, placeholders);
        
        var notificationUrl = string.IsNullOrWhiteSpace(cmd.NotificationUrl)
            ? genNotificationUrl
            : cmd.NotificationUrl;


        var clientState = string.IsNullOrWhiteSpace(cmd.ClientState)
            ? _opts.ClientState
            : cmd.ClientState;


        var expirationUtc =
    cmd.ExpirationUtc == default
        ? DateTime.SpecifyKind(utcNow.AddMinutes(_opts.ExpirationMinutes), DateTimeKind.Utc)
        : DateTime.SpecifyKind(cmd.ExpirationUtc, DateTimeKind.Utc);

        // Resource default + expand {userId}
        var resourceTemplate = string.IsNullOrWhiteSpace(cmd.Resource)
            ? _opts.Resource
            : cmd.Resource;

        var resource = ExpandResource(resourceTemplate, cmd.UserId);

        // Validate
        if (string.IsNullOrWhiteSpace(notificationUrl))
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "NotificationUrl is required.",
                Errors.Validation);

        if (!Uri.TryCreate(notificationUrl, UriKind.Absolute, out _))
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "NotificationUrl must be a valid absolute URL.",
                Errors.Validation);

        if (_opts.RequireClientState && string.IsNullOrWhiteSpace(clientState))
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "ClientState is required.",
                Errors.Validation);

        if (string.IsNullOrWhiteSpace(resource))
            return Result<CreateCalendarSubscriptionResult>.Fail(
                HttpStatusCode.BadRequest,
                "Resource is required.",
                Errors.Validation);

        // Create a NEW request object for the gateway (object initializer works with init-only)
        var gatewayRequest = new CreateCalendarSubscriptionRequest
        {
            UserId = cmd.UserId,
            NotificationUrl = notificationUrl,
            ClientState = clientState,
            Resource = resource,
            ExpirationUtc = expirationUtc
        };

        // Call Graph/SDK
        var res = await _gateway.CreateOrRenewAsync(gatewayRequest, ct);

        // Persist
        var existing = await _repo.GetBySubscriptionIdAsync(res.SubscriptionId, ct);

        var resourceToStore = string.IsNullOrWhiteSpace(res.Resource) ? resource : res.Resource;

        if (existing is null)
        {
            var sub = CalendarSubscription.Create(
                id: res.SubscriptionId,
                subscriptionId: res.SubscriptionId,
                userId: cmd.UserId,
                resource: resourceToStore!,
                notificationUrl: notificationUrl,
                clientState: clientState ?? string.Empty,
                expirationUtc: res.ExpirationUtc.UtcDateTime,
                utcNow: utcNow);

            await _repo.UpsertAsync(sub, ct);
        }
        else
        {
            // Only renew (since your domain doesn’t support endpoint/resource changes yet)
            existing.Renew(res.ExpirationUtc.UtcDateTime, utcNow);
            existing.UpdateWebhook(cmd.NotificationUrl!, cmd.ClientState, utcNow);
            await _repo.UpsertAsync(existing, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<CreateCalendarSubscriptionResult>.Ok(res);
    }

    private static string? ExpandResource(string? template, string userId)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;


        return template.Replace("{userId}", userId, StringComparison.OrdinalIgnoreCase).Trim();
    }
}