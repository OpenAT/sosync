using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace WebSosync.Helpers
{
    public static class DirectoryHelper
    {
        public static void EnsureExistance(string path, ILogger logger)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception)
            {
                logger.LogWarning($"Failed to ensure the existance of \"{path}\".");
            }
        }
    }
}