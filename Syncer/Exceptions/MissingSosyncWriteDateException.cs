using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Exceptions;

namespace Syncer.Exceptions
{
    public class MissingSosyncWriteDateException : SyncerException
    {
        public MissingSosyncWriteDateException(string system, string model, int id)
            : base($"The model [{system}] {model} (ID {id}) has no sosync_write_date.")
        { }
    }
}
