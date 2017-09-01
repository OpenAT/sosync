using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Common.Events
{
    public class RequireRestartEventArgs
    {
        public RequireRestartEventArgs(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; private set; }
    }
}
