using System;
using System.Collections.Generic;

namespace AFH.Booking.Application.Common
{
    public static class UrlTemplateHelper
    {

        public static string Build(string template, IDictionary<string, string> values)
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            string result = template;

            foreach (var kvp in values)
            {
                result = result.Replace($"{{{kvp.Key}}}", kvp.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
