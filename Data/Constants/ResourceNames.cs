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
        public const string SetupDropColumnScript = "SetupDropColumn_SCRIPT";
        public const string ClosePreviousJobsUpdateScript = "ClosePreviousJobs_Update_SCRIPT";
        public const string GetProtocolToSyncSelect = "GetProtocolToSync_SELECT";
        public const string JobStatisticsScript = "JobStatistics_SCRIPT";
        public const string CreateIndex3Script = "CreateIndex3_SCRIPT";
        public const string SkipPreviousJobsIndexScript = "SkipPreviousJobsIndex_SCRIPT";
        public const string CreateProtocolIndexScript = "CreateProtocolIndex_SCRIPT";
    }
}
