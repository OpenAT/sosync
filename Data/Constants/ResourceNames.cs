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
        public const string GetFirstOpenSynJobAndChildren = "GetFirstOpenSynJobAndChildren_SELECT";
        public const string ClosePreviousJobsUpdateScript = "ClosePreviousJobs_Update_SCRIPT";
        public const string UpdateAllParentPaths = "UpdateAllParentPaths";
    }
}
