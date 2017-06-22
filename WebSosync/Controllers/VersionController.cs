using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class VersionController
    {
        #region Constructors
        public VersionController(ILogger<VersionController> logger)
        {
            _log = logger;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns the git commit id
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            // Info to start git, with parameters to query commit id
            var startInfo = new ProcessStartInfo()
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            try
            {
                string result = "";

                // Start the git process and read the commit id
                using (var proc = Process.Start(startInfo))
                {
                    result = proc.StandardOutput.ReadToEnd();
                }
                
                // If an error was returned instead of a commit id log it & return bad request
                if (result.ToLower().StartsWith("fatal:"))
                {
                    _log.LogError("Failed to get Version. Current directory is no git repository.");
                    return new BadRequestObjectResult("Could not read version.");
                }

                // Commit id was retrieved fine, return it
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _log.LogError($"Tried to run \"{startInfo.FileName} {startInfo.Arguments}\"\n{ex.ToString()}");
                return new BadRequestObjectResult("Could not read version.");
            }
        }
        #endregion

        #region Members
        private ILogger _log;
        #endregion
    }
}