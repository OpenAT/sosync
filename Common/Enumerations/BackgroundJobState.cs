using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common.Enumerations
{
    public enum BackgoundJobState
    {
        Stopped = 0,
        Running = 1,
        RunningRestartRequested = 2,
        Stopping = 3,
        Error = 4
    }
}
