using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class MissingFlowRegistrationException : SyncerException
    {
        public MissingFlowRegistrationException(string msg) : base(msg) { }
    }
}
