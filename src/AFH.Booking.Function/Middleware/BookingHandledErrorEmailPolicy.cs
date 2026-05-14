using AFH.Common.Errors.Codes;
using AFH.Common.Errors.Mapping;
using AFH.Common.Errors.Models;

namespace AFH.Booking.Function.Middleware;

internal static class BookingHandledErrorEmailPolicy
{
    public static bool ShouldNotify(ExceptionMappingResult mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return mapping.StatusCode >= 500 || mapping.ErrorCode.Severity == ErrorSeverity.Critical;
    }

    public static ErrorNotificationRequest CreateNotificationRequest(string functionName, int statusCode, ErrorRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(record);

        return new ErrorNotificationRequest
        {
            Subject = $"Booking handled exception in {functionName}",
            Summary = $"Booking handled exception in {functionName}.",
            Severity = record.Severity,
            Record = record,
            Metadata = new Dictionary<string, string?>
            {
                ["service"] = "booking",
                ["functionName"] = functionName,
                ["statusCode"] = statusCode.ToString()
            }
        };
    }
}
