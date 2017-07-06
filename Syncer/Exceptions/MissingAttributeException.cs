using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class MissingAttributeException : SyncerException
    {
        #region Constructors
        public MissingAttributeException(string className, string attributeName)
            : base($"The {attributeName} is required on the {className} class.")
        { }
        #endregion
    }
}
