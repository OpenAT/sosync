using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class TimeDriftException : SyncerException
    {
        public TimeDriftException()
        {
        }

        public TimeDriftException(string message) : base(message)
        {
        }
    }
}
