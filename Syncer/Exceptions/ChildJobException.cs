using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class ChildJobException
        : SyncerException
    {
        public ChildJobException()
        { }

        public ChildJobException(string message) : base(message)
        { }

        public ChildJobException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}
