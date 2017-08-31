using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public static class SosyncError
    {
        public const string Timeout = "timeout";
        public const string RunCounter = "run_counter";
        public const string SyncSource = "sync_source";
        public const string ChildJob = "child_job";
        public const string Transformation = "transformation";
        public const string Cleanup = "cleanup";
        public const string Unknown = "unknown";
    }
}
