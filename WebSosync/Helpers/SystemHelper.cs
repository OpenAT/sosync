using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Helpers
{
    public static class SystemHelper
    {
        public static string WhoAmI()
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "whoami",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string result = "";

            // Start the git process and read the commit id
            using (var proc = Process.Start(startInfo))
            {
                result = proc.StandardOutput.ReadToEnd();

                if (string.IsNullOrEmpty(result))
                    result = proc.StandardError.ReadToEnd();
            }

            result = result.Replace(Environment.NewLine, "");

            return result;
        }
    }
}
