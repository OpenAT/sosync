using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSosync.Converters
{
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd HH:mm:ss.fffffff";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();

            if (s.Contains("T") && s.Contains("Z"))
            {
                s = s.Replace("T", " ").Replace("Z", "");
            }
            return DateTime.ParseExact(s, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
