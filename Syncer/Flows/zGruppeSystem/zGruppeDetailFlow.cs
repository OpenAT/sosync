using System;
using System.Collections.Generic;
using System.Text;
using dadi_data.Models;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    public class zGruppeDetailFlow
        : ReplicateSyncFlow
    {
        public zGruppeDetailFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dbozGruppeDetail>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotImplementedException();
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            if (action == TransformType.CreateNew)
                throw new SyncerException($"{StudioModelName} can only be created from FS, not from FS-Online.");

            throw new NotImplementedException();
        }
    }
}
