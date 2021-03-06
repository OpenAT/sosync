﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Exceptions
{
    public class SyncerException : Exception
    {
        #region Constructors
        public SyncerException() { }
        public SyncerException(string message) : base(message) { }

        public SyncerException(string message, Exception innerException) : base(message, innerException) { }
        #endregion
    }
}
