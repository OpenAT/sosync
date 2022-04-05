using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebSosync.Helpers;

namespace WebSosync.Converters
{
    public class CustomDateTimeNullConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            return DateTimeHelper.ParseSyncerDate(s);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            var result = DateTimeHelper.GetSyncerDateString(value);

            if (result is not null)
            {
                writer.WriteStringValue(result);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
