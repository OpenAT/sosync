using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "")]
    public class zGruppeFlow
        : ReplicateSyncFlow
    {
        public zGruppeFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            throw new NotImplementedException();
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            throw new NotImplementedException();
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            throw new NotImplementedException();
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotImplementedException();
        }
    }
}
