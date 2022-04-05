using System;
using System.Globalization;

namespace WebSosync.Helpers
{
    public static class DateTimeHelper
    {
        private const string SyncerDateFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

        public static DateTime? ParseSyncerDate(string s)
        {
            if (string.IsNullOrEmpty(s) || s.ToLower() == "false")
                return null;

            if (s.Contains("T") && s.Contains("Z"))
            {
                s = s.Replace("T", " ").Replace("Z", "");
            }
            return DateTime.ParseExact(
                s,
                SyncerDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        public static string GetSyncerDateString(DateTime? date)
        {
            if (date is not null)
            {
                return date.Value.ToString(SyncerDateFormat, CultureInfo.InvariantCulture);
            }

            return null;
        }
    }
}
