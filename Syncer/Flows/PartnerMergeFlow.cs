using Syncer.Attributes;
using Syncer.Enumerations;
using System;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    public class PartnerMergeFlow : MergeSyncFlow
    {
        public PartnerMergeFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No chlid jobs, because this direction is not supported
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            RequestChildJob(SosyncSystem.FundraisingStudio, StudioModelName, Job.Job_Source_Merge_Into_Record_ID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            OdooService.Client.MergeModel(
                OnlineModelName,
                Job.Sync_Target_Record_ID.Value,
                Job.Sync_Target_Merge_Into_Record_ID.Value);

            RequestPostTransformChildJob(
                SosyncSystem.FundraisingStudio,
                StudioModelName, 
                Job.Job_Source_Merge_Into_Record_ID.Value,
                true);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException($"Merge from '{OnlineModelName}' to '{StudioModelName}' not supported.");
        }
    }
}
