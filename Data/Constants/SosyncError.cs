using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public static class SosyncError
    {
        public const string Timeout = "timeout";
        public const string RunCounter = "run_counter";
        public const string ChildJobCreation = "child_job_creation";
        public const string ChildJobProcessing = "child_job_processing";
        public const string SourceData = "source_data";
        public const string TargetRequest = "target_request";
        public const string Cleanup = "cleanup";
    }
}
