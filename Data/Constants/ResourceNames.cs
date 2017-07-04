using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Constants
{
    /// <summary>
    /// Defines constants for all resource keys, to avoid using strings when accessing resources.
    /// </summary>
    public static class ResourceNames
    {
        public const string SetupDatabaseScript = "SetupDatabase_SCRIPT";
        public const string GetAllOpenSyncJobsSelect = "GetAllOpenSyncJobs_SELECT";
        public const string GetFirstOpenSynJobAndChildren = "GetFirstOpenSynJobAndChildren_SELECT";
        public const string SetupAddColumnScript = "SetupAddColumn_SCRIPT";
    }
}
