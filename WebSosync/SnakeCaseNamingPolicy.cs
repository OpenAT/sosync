using Newtonsoft.Json.Utilities;
using System.Text.Json;

namespace WebSosync
{
    public class SnakeCaseNamingPolicy
        : JsonNamingPolicy
    {
        public SnakeCaseNamingPolicy()
        { }

        public override string ConvertName(string name)
        {
            return StringUtils.ToSnakeCase(name);
        }
    }
}
