using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Odoo
{
    public static class OdooConvert
    {
        public static int? ToInt32(string value)
        {
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out int result))
                return result;

            return null;
        }

        public static DateTime? ToDateTime(string value, bool isUtc = false)
        {
            DateTimeStyles styles = isUtc ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal : DateTimeStyles.None;

            DateTime? result = null;
            if (!String.IsNullOrEmpty(value) && value != "0")
                result = DateTime.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    styles);

            return result;
        }
    }
}
