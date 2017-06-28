using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common.Enumerations
{
    public enum BackgoundJobState
    {
        Idle = 0,
        Running = 1,
        RunningRestartRequested = 2,
        Stopping = 3,
        Error = 4
    }
}
