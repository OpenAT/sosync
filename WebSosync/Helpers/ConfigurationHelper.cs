using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace WebSosync.Helpers
{
    public static class ConfigurationHelper
    {
        public static string GetIniFile(string[] cmdLineArgs)
        {
            // Build separate configuration from command line
            var tempConfig = new ConfigurationBuilder()
                .AddCommandLine(Program.Args)
                .Build();

            var iniName = $"{tempConfig["instance"]}_sosync.ini";

            return Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), iniName);
        }
    }    
}