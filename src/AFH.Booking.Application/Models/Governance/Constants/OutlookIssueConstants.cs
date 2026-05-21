namespace AFH.Booking.Application.Models.Governance.Constants;

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
