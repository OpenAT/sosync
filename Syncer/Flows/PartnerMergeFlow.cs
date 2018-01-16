using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    public class PartnerMergeFlow : MergeSyncFlow
    {
        public PartnerMergeFlow(IServiceProvider svc) : base(svc)
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
