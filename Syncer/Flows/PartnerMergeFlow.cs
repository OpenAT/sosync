﻿using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using WebSosync.Data;

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
            RequestChildJob(SosyncSystem.FundraisingStudio, StudioModelName, Job.Job_Source_Merge_Into_Record_ID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            OdooService.Client.MergeModel(
                OnlineModelName,
                Job.Sync_Target_Record_ID.Value,
                Job.Sync_Target_Merge_Into_Record_ID.Value);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException($"Merge from '{OnlineModelName}' to '{StudioModelName}' not supported.");
        }
    }
}