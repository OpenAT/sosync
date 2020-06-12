using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class SyncCleanupException
        : SyncerException
    {
        public SyncCleanupException()
        { }

        public SyncCleanupException(string message)
            : base(message)
        { }

        public SyncCleanupException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
