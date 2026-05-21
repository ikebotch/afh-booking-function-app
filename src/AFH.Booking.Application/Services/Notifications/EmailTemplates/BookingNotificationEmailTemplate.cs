using System.Globalization;

namespace AFH.Booking.Application.EmailTemplates;

public static class BookingNotificationEmailTemplate
{
    public static NotificationTemplateContent Build(
        string eventType,
        string? clientDisplayName,
        string? adviserName,
        DateTime startUtc,
        DateTime? endUtc,
        string? timezoneId,
        bool isRemote,
        string? customMessage = null)
    {
        var safeEventType = string.IsNullOrWhiteSpace(eventType) ? "Update" : eventType.Trim();
        var heading = GetHeading(safeEventType);
        var accent = GetAccentColor(safeEventType);
        var tz = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId.Trim();
        var startLocal = FormatLocal(startUtc, tz);
        var endLocal = endUtc.HasValue ? FormatLocal(endUtc.Value, tz) : null;
        var whenLine = endLocal is null ? startLocal : $"{startLocal} to {endLocal}";
        var locationLine = isRemote ? "Remote meeting" : "In-person meeting";
        var greetingName = string.IsNullOrWhiteSpace(clientDisplayName) ? "there" : clientDisplayName.Trim();
        var adviser = string.IsNullOrWhiteSpace(adviserName) ? "your adviser" : adviserName.Trim();
        var note = string.IsNullOrWhiteSpace(customMessage)
            ? "You can review the details in the booking portal."
            : customMessage.Trim();

        var subject = $"AFH Booking: {heading}";
        var textBody = string.Join(
            Environment.NewLine,
            [
                $"Hello {greetingName},",
                string.Empty,
                $"Your booking has been updated: {heading}.",
                $"When: {whenLine}",
                $"Adviser: {adviser}",
                $"Meeting type: {locationLine}",
                string.Empty,
                note,
                string.Empty,
                "This is an automated AFH booking notification."
            ]);

        var calendarDescription = string.Join(
            Environment.NewLine,
            [
                $"AFH Booking - {heading}",
                $"When: {whenLine}",
                $"Adviser: {adviser}",
                $"Meeting type: {locationLine}",
                $"Note: {note}"
            ]);

        var htmlBody = $@"
<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>{Escape(subject)}</title>
  </head>
  <body style=""margin:0;padding:0;background-color:#f3f6fa;font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#f3f6fa;padding:28px 16px;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""640"" cellspacing=""0"" cellpadding=""0"" style=""width:100%;max-width:640px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 8px 24px rgba(2,6,23,0.08);"">
            <tr>
              <td style=""background:linear-gradient(120deg,{accent},#0f172a);padding:22px 24px;color:#ffffff;"">
                <div style=""font-size:12px;letter-spacing:0.08em;text-transform:uppercase;opacity:0.9;"">AFH Booking</div>
                <h1 style=""margin:8px 0 0;font-size:24px;line-height:1.2;"">{Escape(heading)}</h1>
              </td>
            </tr>
            <tr>
              <td style=""padding:24px;"">
                <p style=""margin:0 0 16px;font-size:16px;line-height:1.5;"">Hello {Escape(greetingName)},</p>
                <p style=""margin:0 0 18px;font-size:15px;line-height:1.6;color:#334155;"">
                  Your appointment has been updated. The latest details are below.
                </p>
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;"">
                  <tr>
                    <td style=""padding:10px 14px;background:#f8fafc;font-size:13px;color:#475569;width:34%;"">Event</td>
                    <td style=""padding:10px 14px;font-size:14px;color:#0f172a;font-weight:600;"">{Escape(safeEventType)}</td>
                  </tr>
                  <tr>
                    <td style=""padding:10px 14px;background:#f8fafc;font-size:13px;color:#475569;"">When</td>
                    <td style=""padding:10px 14px;font-size:14px;color:#0f172a;"">{Escape(whenLine)}</td>
                  </tr>
                  <tr>
                    <td style=""padding:10px 14px;background:#f8fafc;font-size:13px;color:#475569;"">Adviser</td>
                    <td style=""padding:10px 14px;font-size:14px;color:#0f172a;"">{Escape(adviser)}</td>
                  </tr>
                  <tr>
                    <td style=""padding:10px 14px;background:#f8fafc;font-size:13px;color:#475569;"">Meeting type</td>
                    <td style=""padding:10px 14px;font-size:14px;color:#0f172a;"">{Escape(locationLine)}</td>
                  </tr>
                </table>
                <div style=""margin:18px 0 0;padding:14px 16px;background:#eef6ff;border-left:4px solid {accent};border-radius:8px;font-size:14px;line-height:1.6;color:#1e293b;"">
                  {Escape(note)}
                </div>
              </td>
            </tr>
            <tr>
              <td style=""padding:16px 24px;background:#f8fafc;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b;"">
                This is an automated AFH booking notification.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";

        return new NotificationTemplateContent(subject, htmlBody, textBody, calendarDescription);
    }

    private static string GetHeading(string eventType)
    {
        return eventType.Trim().ToLowerInvariant() switch
        {
            "cancel" or "cancelled" => "Appointment Cancelled",
            "rearrange" or "rescheduled" => "Appointment Rescheduled",
            "confirmed" => "Appointment Confirmed",
            _ => "Appointment Updated"
        };
    }

    private static string GetAccentColor(string eventType)
    {
        return eventType.Trim().ToLowerInvariant() switch
        {
            "cancel" or "cancelled" => "#b42318",
            "rearrange" or "rescheduled" => "#0b6dd8",
            "confirmed" => "#047857",
            _ => "#2563eb"
        };
    }

    private static string FormatLocal(DateTime utc, string timezoneId)
    {
        try
        {
            if (timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
                return FormatUtc(utc);

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
            return local.ToString("ddd dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) + $" ({timezoneId})";
        }
        catch
        {
            return FormatUtc(utc);
        }
    }

    private static string FormatUtc(DateTime utc)
        => utc.ToUniversalTime().ToString("ddd dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}

public sealed record NotificationTemplateContent(
    string Subject,
    string HtmlBody,
    string TextBody,
    string CalendarDescription);
