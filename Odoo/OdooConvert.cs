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

        /// <summary>
        /// Converts a nullable date/time to a string with a fixed format,
        /// or boolean false if it is null.
        /// </summary>
        /// <param name="value">The value to be converted.</param>
        /// <returns></returns>
        public static object ToStringOrBoolFalse(DateTime? value)
        {
            object result = (object)false;

            if (value.HasValue)
                result = value.Value.ToString("yyyy-MM-dd");

            return result;
        }
    }
}
