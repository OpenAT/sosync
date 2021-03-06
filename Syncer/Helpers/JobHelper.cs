﻿using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Helpers
{
    public static class JobHelper
    {
        public const int MaxJobRunCount = 10;

        public static void SetJobError(SyncJob job, SosyncError error, string errorText, bool useErrorRetry = true)
        {
            if (useErrorRetry)
                // Use error_retry as long MaxJobRunCount is not reached
                job.Job_State = (job.Job_Run_Count < MaxJobRunCount ? SosyncState.ErrorRetry.Value : SosyncState.Error.Value);
            else
                job.Job_State = SosyncState.Error.Value;

            job.Job_End = DateTime.UtcNow;
            job.Job_Error_Code = error.Value;
            job.Job_Error_Text = (string.IsNullOrEmpty(job.Job_Error_Text) ? "" : job.Job_Error_Text + "\n\n") + errorText;
            job.Write_Date = DateTime.UtcNow;
        }
    }
}
