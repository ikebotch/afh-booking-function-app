using AFH.Booking.Application.Abstractions.Calendar.Subscription;
using AFH.Booking.Application.Common;
using AFH.Booking.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AFH.Booking.Infrastructure.Calendar;

public sealed class CalendarSubscriptionGateway : ICalendarSubscriptionGateway
{
    private readonly GraphServiceClient _graph;
    private readonly GraphWebhookOptions _opts;
    private readonly ILogger<CalendarSubscriptionGateway> _logger;

    public CalendarSubscriptionGateway(
        GraphServiceClient graph,
        IOptions<GraphWebhookOptions> opts,
        ILogger<CalendarSubscriptionGateway> logger)
    {
        _graph = graph;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<CreateCalendarSubscriptionResult> CreateOrRenewAsync(
        CreateCalendarSubscriptionRequest request,
        CancellationToken ct)
    {
        var result = await CreateInternalAsync(request, ct);
        if (!result.IsSuccess || result.Value is null)
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to create calendar subscription.");

        return result.Value;
    }

    public Task<Result<CreateCalendarSubscriptionResult>> CreateAsync(
        CreateCalendarSubscriptionRequest request,
        CancellationToken ct)
        => CreateInternalAsync(request, ct);

    public async Task<Result> DeleteAsync(string subscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Result.Fail(System.Net.HttpStatusCode.BadRequest, "subscriptionId is required.", "Validation");

        try
        {
            await _graph.Subscriptions[subscriptionId].DeleteAsync(cancellationToken: ct);
            _logger.LogInformation("Deleted Graph subscription. SubscriptionId={SubscriptionId}", subscriptionId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Graph subscription {SubscriptionId}", subscriptionId);
            return Result.Fail(System.Net.HttpStatusCode.BadGateway, "Failed to delete Graph subscription.", "GraphError");
        }
    }

    private async Task<Result<CreateCalendarSubscriptionResult>> CreateInternalAsync(
        CreateCalendarSubscriptionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Result<CreateCalendarSubscriptionResult>.Fail(System.Net.HttpStatusCode.BadRequest, "AdviserUserId is required.", "Validation");

        var notificationUrl = string.IsNullOrWhiteSpace(request.NotificationUrl)
            ? _opts.NotificationUrl
            : request.NotificationUrl;

        var clientState = string.IsNullOrWhiteSpace(request.ClientState)
            ? _opts.ClientState
            : request.ClientState;

        if (string.IsNullOrWhiteSpace(notificationUrl))
            return Result<CreateCalendarSubscriptionResult>.Fail(System.Net.HttpStatusCode.BadRequest, "NotificationUrl is required.", "Validation");

        if (string.IsNullOrWhiteSpace(clientState))
            return Result<CreateCalendarSubscriptionResult>.Fail(System.Net.HttpStatusCode.BadRequest, "ClientState is required.", "Validation");

        var resource = request.Resource?.Replace("{userId}", request.UserId, StringComparison.OrdinalIgnoreCase)
                       ?? $"/users/{request.UserId}/events";

        var expiry = request.ExpirationUtc == default
            ? DateTimeOffset.UtcNow.AddMinutes(Math.Max(15, _opts.ExpirationMinutes))
            : request.ExpirationUtc;

        var sub = new Subscription
        {
            ChangeType = "created,updated,deleted",
            NotificationUrl = notificationUrl,
            Resource = resource,
            ExpirationDateTime = expiry,
            ClientState = clientState
        };

        try
        {
            var created = await _graph.Subscriptions.PostAsync(sub, cancellationToken: ct);

            if (created?.Id is null)
                return Result<CreateCalendarSubscriptionResult>.Fail(System.Net.HttpStatusCode.BadGateway, "Graph did not return a subscription id.", "GraphError");

            _logger.LogInformation(
                "Created Graph subscription. AdviserUserId={AdviserUserId} SubscriptionId={SubscriptionId} Expiry={Expiry}",
                request.UserId,
                created.Id,
                created.ExpirationDateTime);

            return Result<CreateCalendarSubscriptionResult>.Ok(new CreateCalendarSubscriptionResult
            {
                SubscriptionId = created.Id,
                ExpirationUtc = created.ExpirationDateTime ?? expiry,
                Resource = created.Resource
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph subscription for AdviserUserId={AdviserUserId}", request.UserId);
            return Result<CreateCalendarSubscriptionResult>.Fail(System.Net.HttpStatusCode.BadGateway, "Failed to create Graph subscription.", "GraphError");
        }
    }
}
