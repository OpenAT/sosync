using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class JobDateMismatchException
        : SyncerException
    {
        public JobDateMismatchException(string msg)
            : base(msg)
        {
        }
    }
}
