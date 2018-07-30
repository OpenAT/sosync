using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    public class zGruppeDetailDeleteFlow
        : DeleteSyncFlow
    {
        public zGruppeDetailDeleteFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new SyncerException($"{StudioModelName} can only be deleted from FS, not from FS-Online.");
        }
    }
}
