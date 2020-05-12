using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    public class PartnerMergeFlow : MergeSyncFlow
    {
        public PartnerMergeFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No chlid jobs, because this direction is not supported
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            RequestChildJob(SosyncSystem.FundraisingStudio, StudioModelName, Job.Job_Source_Merge_Into_Record_ID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            Svc.OdooService.Client.MergeModel(
                OnlineModelName,
                Job.Sync_Target_Record_ID.Value,
                Job.Sync_Target_Merge_Into_Record_ID.Value);

            RequestPostTransformChildJob(
                SosyncSystem.FundraisingStudio,
                StudioModelName, 
                Job.Job_Source_Merge_Into_Record_ID.Value,
                true,
                SosyncJobSourceType.Default);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException($"Merge from '{OnlineModelName}' to '{StudioModelName}' not supported.");
        }
    }
}
