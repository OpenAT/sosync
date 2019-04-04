using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Helpers
{
    public static class JobHelper
    {
        public const int MaxJobRunCount = 10;

        public static void SetJobError(SyncJob job, string errorCode, string errorText)
        {
            job.Job_State = (job.Job_Run_Count < MaxJobRunCount ? SosyncState.ErrorRetry : SosyncState.Error);
            job.Job_End = DateTime.UtcNow;
            job.Job_Error_Code = errorCode;
            job.Job_Error_Text = (string.IsNullOrEmpty(job.Job_Error_Text) ? "" : job.Job_Error_Text + "\n\n") + errorText;
            job.Write_Date = DateTime.UtcNow;
        }
    }
}
