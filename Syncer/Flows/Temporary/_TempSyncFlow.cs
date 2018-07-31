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

namespace Syncer.Flows.Temporary
{
    /// <summary>
    /// Base class for sync flows that handle special cases like data correction etc.
    /// </summary>
    public abstract class TempSyncFlow : SyncFlow
    {
        public TempSyncFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
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
                SetProcessInfos(OnlineModelName, Job);
            else
                // If source is online, set merge IDs via studio
                SetProcessInfos(StudioModelName, Job);

            Stopwatch consistencyWatch = new Stopwatch();

            HandleChildJobs(
                "Child Job",
                RequiredChildJobs, 
                flowService, 
                null, 
                consistencyWatch, 
                ref requireRestart,
                ref restartReason);

            var description = $"Processing [{Job.Sync_Target_System}] {Job.Sync_Target_Model} {Job.Sync_Target_Record_ID} and {Job.Sync_Target_Merge_Into_Record_ID}";

            HandleTransformation(description, null, consistencyWatch, ref requireRestart, ref restartReason);

            HandleChildJobs(
                "Post Transformation Child Job",
                RequiredChildJobs,
                flowService,
                null,
                consistencyWatch,
                ref requireRestart,
                ref restartReason);
        }

        private void SetProcessInfos(string modelName, SyncJob job)
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
                    var targetStudioID = GetStudioIDFromMssqlViaOnlineID(modelName, MdbService.GetStudioModelIdentity(modelName), sourceOnlineID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceOnlineID;
                    job.Sync_Target_Record_ID = targetStudioID;

                    UpdateJob(Job, "Updating IDs");
                }
                else
                {
                    job.Sync_Source_System = SosyncSystem.FundraisingStudio;
                    job.Sync_Target_System = SosyncSystem.FSOnline;

                    job.Sync_Source_Model = StudioModelName;
                    job.Sync_Target_Model = OnlineModelName;

                    var sourceStudioID = job.Job_Source_Record_ID;
                    var targetOnlineID = GetOnlineIDFromOdooViaStudioID(modelName, sourceStudioID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceStudioID;
                    job.Sync_Target_Record_ID = targetOnlineID;

                    UpdateJob(Job, "Updating IDs");
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
