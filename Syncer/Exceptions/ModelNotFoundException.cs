using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Exceptions;

namespace Syncer.Exceptions
{
    public class ModelNotFoundException : SyncerException
    {
        public ModelNotFoundException(string system, string model, int id)
            : base($"The model [{system}] {model} (ID {id}) did not exist.")
        { }
    }
}
