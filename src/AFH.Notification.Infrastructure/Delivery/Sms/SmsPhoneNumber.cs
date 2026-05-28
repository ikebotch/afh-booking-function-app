using System.Text.RegularExpressions;

namespace AFH.Notification.Infrastructure.Delivery.Sms;

public static partial class SmsPhoneNumber
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        candidate = candidate.Replace("-", string.Empty, StringComparison.Ordinal);
        candidate = candidate.Replace("(", string.Empty, StringComparison.Ordinal);
        candidate = candidate.Replace(")", string.Empty, StringComparison.Ordinal);

        if (!E164Regex().IsMatch(candidate))
            return false;

        normalized = candidate;
        return true;
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164Regex();
}
