using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Helpers
{
    public static class SharePointFieldExtensions
    {
        public static string GetString(this IDictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out var value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;
        }

        public static bool GetBoolEquals(this IDictionary<string, object> data, string key, string expected)
        {
            return data.TryGetValue(key, out var value)
                && string.Equals(value?.ToString(), expected, StringComparison.OrdinalIgnoreCase);
        }


        public static int GetInt(this IDictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var result))
            {
                return result;
            }
            return 0; // default if missing or not parsable
        }

        public static DateTime GetDateTime(this IDictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var value) && DateTime.TryParse(value?.ToString(), out var result))
            {
                return result;
            }
            return DateTime.MinValue; // default if missing or not parsable
        }

        public static Guid GetGuid(this IDictionary<string, object> data, string key)
        {
            if (data.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var result))
            {
                return result;
            }
            return Guid.Empty; // default if missing or not parsable
        }
    }
}
