using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

namespace WebSosync.Converters.Tests
{
    public class DictionaryConverterTests
    {
        public static IEnumerable<object?[]> ConvertWorksData
        {
            get
            {
                yield return new object?[] { "{\"k\": \"v\"}", new Dictionary<string, string>() { ["k"] = "v" } };
                yield return new object?[] { "{\"k1\": \"v1\", \"k2\": \"v2\"}", new Dictionary<string, string>() { ["k1"] = "v1", ["k2"] = "v2" } };
                yield return new object?[] { "{\"k\": 1}", new Dictionary<string, string>() { ["k"] = "1" } };
                yield return new object?[] { "{\"k\": true}", new Dictionary<string, string>() { ["k"] = "True" } };
                yield return new object?[] { "{\"k\": false}", new Dictionary<string, string>() { ["k"] = "False" } };
                yield return new object?[] { "{}", new Dictionary<string, string>() };
            }
        }

        [Theory]
        [MemberData(nameof(ConvertWorksData))]
        public void ConvertWorks(string json, Dictionary<string, string> expected)
        {
            var converter = new DictionaryConverter();
            var options = new JsonSerializerOptions();
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            reader.Read();

            var actual = converter.Read(ref reader, typeof(Dictionary<string, string>), options);

            Assert.Equal(expected, actual);
        }
    }
}
