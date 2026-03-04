using AFH.Booking.Application.Calendar.Options;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Webhooks;
using Microsoft.Extensions.Options;
using System.Net;

namespace AFH.Booking.Application.Calendar.Notifications;

public sealed class CalendarNotificationHandler : ICalendarNotificationHandler
{
    private readonly GraphWebhookOptions _opts;

    public CalendarNotificationHandler(IOptions<GraphWebhookOptions> opts)
    {
        _opts = opts.Value;
    }

    public Task<Result> HandleAsync(GraphNotificationEnvelope? envelope, CancellationToken ct)
    {
        var items = envelope?.Value ?? [];

        foreach (var n in items)
        {
            if (!string.Equals(n.ClientState, _opts.ClientState, StringComparison.Ordinal))
                return Task.FromResult(Result.Unauthorized("Invalid clientState."));

            // TODO: enqueue processing
        }

        return Task.FromResult(Result.Ok());
    }
}