using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class MissingForeignKeyException : SyncerException
    {
        public MissingForeignKeyException(string msg) : base(msg) { }
    }
}
