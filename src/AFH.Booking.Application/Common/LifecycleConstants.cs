namespace AFH.Booking.Application.Common;

public static class LifecycleEventTypes
{
    public const string Booked = "Booked";
    public const string Cancelled = "Cancelled";
    public const string Rearranged = "Rearranged";
    public const string ReArranged = "ReArranged";
    public const string NoShow = "No Show";
}

public static class LifecycleStates
{
    public const string Booked = "Booked";
    public const string Rearranged = "Rearranged";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "No Show";
}

public static class LifecycleActors
{
    public const string Client = "Client";
    public const string LeadTech = "LeadTech";
    public const string Adviser = "Adviser";
    public const string System = "System";
    public const string Unknown = "Unknown";
}

public static class LifecycleStepNames
{
    public const string Outlook = "Outlook";
    public const string SqlAudit = "SqlAudit";
    public const string Notifications = "Notifications";
}

public static class LifecycleStepStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public static class LifecycleErrorCodes
{
    public const string CalendarCancelFailed = "CalendarCancelFailed";
    public const string CalendarCreateFailed = "CalendarCreateFailed";
    public const string NotificationFailed = "NotificationFailed";
}

public static class OutlookIssueTypes
{
    public const string Conflict = "Conflict";
    public const string Hygiene = "Hygiene";
    public const string Governance = "Governance";
    public const string Delivery = "Delivery";
}

public static class OutlookIssueCodes
{
    public const string IncorrectShowAs = "IncorrectShowAs";
    public const string DoubleBookedEvent = "DoubleBookedEvent";
    public const string InvalidRecurrencePattern = "InvalidRecurrencePattern";
    public const string MissingLocation = "MissingLocation";
    public const string DeletionAttemptDetected = "DeletionAttemptDetected";
    public const string EventTamperingDetected = "EventTamperingDetected";
    public const string ControlledReconciliationRequired = "ControlledReconciliationRequired";
}

public static class OperationalIssueStatuses
{
    public const string Open = "Open";
    public const string Escalated = "Escalated";
    public const string ReconciliationRequired = "ReconciliationRequired";
}
