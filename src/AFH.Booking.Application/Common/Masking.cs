using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Booking.Application.Common;

public class Masking
{

    public static string MaskName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Always keep first 2 characters, then 4 stars
        return name.Length <= 2
            ? name
            : name.Substring(0, 2) + "****";
    }

    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        int atIndex = email.IndexOf('@');
        if (atIndex < 2) return email;

        // Local part: first 2 chars + 4 stars
        string localPart = email.Substring(0, 2) + "****";

        string domain = email.Substring(atIndex + 1);
        int dotIndex = domain.IndexOf('.');
        if (dotIndex < 1) return email;

        // Domain: first char + 4 stars + suffix
        string domainPrefix = domain.Substring(0, 1) + "****";
        string domainSuffix = domain.Substring(dotIndex);

        return $"{localPart}@{domainPrefix}{domainSuffix}";
    }
}
