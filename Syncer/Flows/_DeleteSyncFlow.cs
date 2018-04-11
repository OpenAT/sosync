using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    public abstract class DeleteSyncFlow : SyncFlow
    {
        public DeleteSyncFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
        }

        protected override void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            CheckRunCount(MaxRunCount);

            if (Job.Job_Source_System == SosyncSystem.FundraisingStudio)
                SetDeleteInfos(OnlineModelName, Job);
            else
                SetDeleteInfos(StudioModelName, Job);

            Stopwatch consistencyWatch = new Stopwatch();

            HandleChildJobs(
                "Child Job",
                RequiredChildJobs, 
                flowService, 
                null, 
                consistencyWatch,
                ref requireRestart, 
                ref restartReason);

            var description = $"Deleting [{Job.Sync_Target_System}] {Job.Sync_Target_Model} {Job.Sync_Target_Record_ID} (Source: [{Job.Sync_Source_System}] {Job.Sync_Source_Model} {Job.Sync_Source_Record_ID})";
            HandleTransformation(description, null, consistencyWatch, ref requireRestart, ref restartReason);

            HandleChildJobs(
                "Post Transformation Child Job",
                RequiredPostTransformChildJobs,
                flowService,
                null,
                consistencyWatch,
                ref requireRestart,
                ref restartReason);
        }

        private void SetDeleteInfos(string modelName, SyncJob job)
        {
            using (var db = GetDb())
            {
                if (job.Job_Source_System == SosyncSystem.FSOnline)
                {
                    job.Sync_Source_System = SosyncSystem.FSOnline;
                    job.Sync_Target_System = SosyncSystem.FundraisingStudio;

                    job.Sync_Source_Model = OnlineModelName;
                    job.Sync_Target_Model = StudioModelName;

                    var sourceOnlineID = job.Job_Source_Record_ID; 
                    var targetStudioID = GetFsIdByFsoId(modelName, MdbService.GetStudioModelIdentity(StudioModelName), sourceOnlineID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceOnlineID;
                    job.Sync_Target_Record_ID = targetStudioID;

                    UpdateJob(Job, "Updating Delete-IDs");
                }
                else
                {
                    job.Sync_Source_System = SosyncSystem.FundraisingStudio;
                    job.Sync_Target_System = SosyncSystem.FSOnline;

                    job.Sync_Source_Model = StudioModelName;
                    job.Sync_Target_Model = OnlineModelName;

                    var sourceStudioID = job.Job_Source_Record_ID;
                    var targetOnlineID = GetFsoIdByFsId(modelName, sourceStudioID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceStudioID;
                    job.Sync_Target_Record_ID = targetOnlineID;

                    UpdateJob(Job, "Updating Delete-IDs");
                }
            }
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            // Not applicable for delete flows
            return null;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Not applicable for delete flows
            return null;
        }
    }
}
