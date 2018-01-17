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

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No chlid jobs
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException($"Merge from '{OnlineModelName}' to '{StudioModelName}' not supported.");
        }
    }
}
