using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace WebSosync.Helpers
{
    public static class LogHelper
    {
        /// <summary>
        /// Converts a <see cref="LogLevel"/> to a <see cref="LogEventLevel"/>.
        /// </summary>
        /// <param name="lvl">The <see cref="LogLevel"/> to convert.</param>
        /// <returns>Returns a <see cref="LogEventLevel"/>.</returns>
        public static LogEventLevel ConvertLevel(LogLevel lvl)
        {
            LogEventLevel result;

            switch (lvl)
            {
                case LogLevel.None: result = LogEventLevel.Error; break;
                case LogLevel.Debug: result = LogEventLevel.Debug; break;
                case LogLevel.Trace: result = LogEventLevel.Verbose; break;
                case LogLevel.Information: result = LogEventLevel.Information; break;
                case LogLevel.Warning: result = LogEventLevel.Warning; break;
                case LogLevel.Error: result = LogEventLevel.Error; break;
                case LogLevel.Critical: result = LogEventLevel.Fatal; break;
                default: result = LogEventLevel.Error; break;
            }

            return result;
        }
    }
}