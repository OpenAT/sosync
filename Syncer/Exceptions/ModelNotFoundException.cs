using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Exceptions;
using WebSosync.Data;

namespace Syncer.Exceptions
{
    public class ModelNotFoundException : SyncerException
    {
        public ModelNotFoundException(SosyncSystem system, string model, int id)
            : base($"The model [{system?.Value}] {model} (ID {id}) did not exist.")
        { }
    }
}
