using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class ConsistencyException
        : SyncerException
    {
        public ConsistencyException(string msg)
            : base(msg)
        {
        }
    }
}
