using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common
{
    public static class DateTimeHelper
    {
        public static DateTime? ToUtc(DateTime? localTime)
        {
            if (localTime.HasValue)
                return localTime.Value.ToUniversalTime();

            return null;
        }

        public static DateTime? ToLocal(DateTime? universalTime)
        {
            if (universalTime.HasValue)
                return universalTime.Value.ToLocalTime();

            return null;
        }
    }
}
