using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Services
{
    public class OdooFormatService
    {
        public object ToString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            return s;
        }

        public object ToDateTime(DateTime? d)
        {
            if (!d.HasValue)
                return false;

            return $"{d.Value.ToString("yyyy-MM-dd")}T{d.Value.ToString("HH:mm:ss.fff")}";
        }
    }
}
