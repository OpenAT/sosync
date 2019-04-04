using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public class SosyncError
    {
        public string Value { get; private set; }

        public static SosyncError Timeout { get; private set; }
        public static SosyncError RunCounter { get; private set; }
        public static SosyncError SyncSource { get; private set; }
        public static SosyncError ChildJob { get; private set; }
        public static SosyncError Transformation { get; private set; }
        public static SosyncError Cleanup { get; private set; }
        public static SosyncError Unknown { get; private set; }

        static SosyncError()
        {
            Timeout = new SosyncError("timeout");
            RunCounter = new SosyncError("run_counter");
            SyncSource = new SosyncError("sync_source");
            ChildJob = new SosyncError("child_job");
            Transformation = new SosyncError("transformation");
            Cleanup = new SosyncError("cleanup");
            Unknown = new SosyncError("unknown");
        }

        private SosyncError(string value)
        {
            Value = value;
        }
    }
}
