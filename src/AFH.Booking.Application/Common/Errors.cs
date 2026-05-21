namespace AFH.Booking.Application.Common;

public static class Errors
{
    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
    public const string Conflict = "Conflict";
    public const string Unauthorized = "Unauthorized";
    public const string Forbidden = "Forbidden";
    public const string ServerError = "ServerError";
    public const string HoldCancelled = "HoldCancelled";
    public const string HoldExpired = "HoldExpired";
    public const string HoldAlreadyConfirmed = "HoldAlreadyConfirmed";
    public const string HoldSlotMissing = "HoldSlotMissing";
    public const string HoldTransactionMissing = "HoldTransactionMissing";
    public const string HoldStateInvalid = "HoldStateInvalid";
    public const string ReasonCodeRequired = "ReasonCodeRequired";
    public const string BookingConflictOverlap = "BookingConflictOverlap";
    public const string BookingConflictBufferViolation = "BookingConflictBufferViolation";
    public const string BookingConflictDoubleBooked = "BookingConflictDoubleBooked";
    public const string BookingConflictShowAs = "BookingConflictShowAs";
    public const string BookingConflictRecurrence = "BookingConflictRecurrence";
    public const string ExactRouteTimeUnavailable = "ExactRouteTimeUnavailable";
}
