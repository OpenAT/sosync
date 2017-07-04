using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class RunCountException : SyncerException
    {
        #region Constructors
        public RunCountException(int current, int max)
            : base($"Run counter reached {current} of {max}.")
        { }
        #endregion
    }
}
