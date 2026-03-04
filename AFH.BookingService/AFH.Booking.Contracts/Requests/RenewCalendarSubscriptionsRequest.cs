namespace AFH.Common.CalendarUtils.Sdk.Contracts.Requests;

public sealed class RenewCalendarSubscriptionsRequest
{
    public int? RenewWithinMinutes { get; set; } = 60;
}