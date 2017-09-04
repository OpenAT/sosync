using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common
{
    public static class SpecialFormat
    {
        public static string FromMilliseconds(int ms)
        {
            var t = new TimeSpan(0, 0, 0, 0, ms);

            if (t.TotalDays > 1)
                return $"{t.TotalDays.ToString("0")}d";

            if (t.TotalHours > 1)
                return $"{t.TotalHours.ToString("0")}h";

            if (t.TotalMinutes > 1)
                return $"{t.TotalMinutes.ToString("0")}min";

            if (t.TotalSeconds > 1)
                return $"{t.TotalSeconds.ToString("0")}sec";

            return $"{ms}ms";
        }
    }
}
