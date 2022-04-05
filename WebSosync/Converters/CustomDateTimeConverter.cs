using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebSosync.Helpers;

namespace WebSosync.Converters
{
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var s = reader.GetString();
            var result = DateTimeHelper.ParseSyncerDate(s);

            if (result is null)
            {
                throw new NullReferenceException("Date could not be parsed for a non-nullable DateTime field.");
            }

            return result.Value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(DateTimeHelper.GetSyncerDateString(value));
        }
    }
}
