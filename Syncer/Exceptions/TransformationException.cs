using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class TransformationException
        : SyncerException
    {
        public TransformationException()
        { }

        public TransformationException(string message) : base(message)
        { }

        public TransformationException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}
