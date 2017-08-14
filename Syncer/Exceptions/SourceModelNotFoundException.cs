using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Exceptions;

namespace Syncer.Exceptions
{
    public class SourceModelNotFoundException : SyncerException
    {
        public SourceModelNotFoundException(string system, string model, int id)
            : base($"The source model [{system}] {model} (ID {id}) did not exist.")
        { }
    }
}
