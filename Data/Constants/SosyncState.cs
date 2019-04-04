using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data
{
    public class SosyncState
    {
        public string Value { get; private set; }

        public static SosyncState New { get; private set; }
        public static SosyncState InProgress { get; private set; }
        public static SosyncState Done { get; private set; }
        public static SosyncState Error { get; private set; }
        public static SosyncState ErrorRetry { get; private set; }
        public static SosyncState Skipped { get; private set; }

        static SosyncState()
        {
            New = new SosyncState("new");
            InProgress = new SosyncState("inprogress");
            Done = new SosyncState("done");
            Error = new SosyncState("error");
            ErrorRetry = new SosyncState("error_retry");
            Skipped = new SosyncState("skipped");
        }

        private SosyncState(string value)
        {
            Value = value;
        }
    }
}
