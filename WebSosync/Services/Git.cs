﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Services
{
    public class Git
    {
        #region Methods
        public string GetCommitId()
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
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

            // If an error was returned, throw an exception
            if (result.ToLower().StartsWith("fatal:"))
                throw new Exception(result);

            return result;
        }
        #endregion
    }
}
