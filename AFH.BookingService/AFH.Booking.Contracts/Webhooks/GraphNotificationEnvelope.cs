using AFH.Common.CalendarUtils.Sdk.Contracts.Webhooks;

namespace AFH.Booking.Contracts.Webhooks;

public sealed class GraphNotificationEnvelope
{
    public IReadOnlyList<GraphNotificationItem>? Value { get; set; }
}