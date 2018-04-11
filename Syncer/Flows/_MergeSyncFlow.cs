using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Data.Models;
using System.Diagnostics;
using WebSosync.Data;
using Syncer.Exceptions;
using Microsoft.Extensions.Logging;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for sync flows that deal with merging duplicate data.
    /// </summary>
    public abstract class MergeSyncFlow : SyncFlow
    {
        public MergeSyncFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
        }

        /// <summary>
        /// Starts the sync flow.
        /// </summary>
        /// <param name="flowService">The flow service for handling child jobs.</param>
        /// <param name="job">The job that initiated this sync flow.</param>
        /// <param name="loadTimeUTC">The loading time of the job.</param>
        /// <param name="requireRestart">Reference parameter to indicate that the syncer should restart immediately after this flow ends.</param>
        /// <param name="restartReason">Reference parameter to indicate the reason why the restart was requested.</param>
        protected override void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            CheckRunCount(MaxRunCount);

            if (Job.Job_Source_System == SosyncSystem.FundraisingStudio)
                // If source is studio, set merge IDs via online
                SetMergeInfos(OnlineModelName, Job);
            else
                // If source is online, set merge IDs via studio
                SetMergeInfos(StudioModelName, Job);

            Stopwatch consistencyWatch = new Stopwatch();

            HandleChildJobs(
                "Child Job",
                RequiredChildJobs, 
                flowService, 
                null, 
                consistencyWatch, 
                ref requireRestart,
                ref restartReason);

            var description = $"Merging [{Job.Sync_Target_System}] {Job.Sync_Target_Model} {Job.Sync_Target_Record_ID} into {Job.Sync_Target_Merge_Into_Record_ID}";
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

        private void SetMergeInfos(string modelName, SyncJob job)
        {
            using (var db = GetDb())
            {
                if (job.Job_Source_System == SosyncSystem.FSOnline)
                {
                    throw new SyncerException("Merging from 'fso' to 'fs' currently not supported.");
                }
                else
                {
                    job.Sync_Source_System = SosyncSystem.FundraisingStudio;
                    job.Sync_Target_System = SosyncSystem.FSOnline;

                    job.Sync_Source_Model = StudioModelName;
                    job.Sync_Target_Model = OnlineModelName;

                    var sourceStudioID = job.Job_Source_Record_ID;
                    var sourceOnlineID = GetFsoIdByFsId(modelName, sourceStudioID) ?? job.Job_Source_Target_Record_ID;

                    var mergeStudioID = job.Job_Source_Merge_Into_Record_ID;
                    var mergeOnlineID = GetFsoIdByFsId(modelName, mergeStudioID.Value) ?? job.Job_Source_Merge_Into_Record_ID;

                    job.Sync_Source_Record_ID = sourceStudioID;
                    job.Sync_Source_Merge_Into_Record_ID = mergeStudioID;

                    job.Sync_Target_Record_ID = sourceOnlineID;
                    job.Sync_Target_Merge_Into_Record_ID = mergeOnlineID;

                    UpdateJob(Job, "Updating Merge-IDs");
                }
            }
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            // Not applicable for merge flows
            return null;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Not applicable for merge flows
            return null;
        }
    }
}
