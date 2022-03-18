using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WebSosync.Converters
{
    public class DictionaryConverter
        : JsonConverter<Dictionary<string, string>>
    {
        public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new Dictionary<string, string>();

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var name = reader.GetString();
                        reader.Read();

                        var value = reader.TokenType switch
                        {
                            JsonTokenType.String => reader.GetString(),
                            JsonTokenType.Number => reader.GetInt32().ToString("0"),
                            JsonTokenType.True => reader.GetBoolean().ToString(),
                            JsonTokenType.False => reader.GetBoolean().ToString(),
                            _ => (string)null
                        };

                        result.Add(name, value);
                    }

                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
