using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class SyncerDeletionFailedException
        : SyncerException
    {
        #region Constructors
        public SyncerDeletionFailedException(string message)
            : base(message)
        { }
        #endregion
    }
}
