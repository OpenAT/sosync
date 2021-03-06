﻿using System;
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
using WebSosync.Common;
using Syncer.Helpers;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for sync flows that deal with merging duplicate data.
    /// </summary>
    public abstract class MergeSyncFlow : SyncFlow
    {
        public MergeSyncFlow(SyncServiceCollection svc)
            : base(svc)
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
            CheckRunCount(JobHelper.MaxJobRunCount);

            if (Job.Job_Source_System == SosyncSystem.FundraisingStudio.Value)
                // If source is studio, set merge IDs via online
                SetMergeInfos(OnlineModelName, Job);
            else
                // If source is online, set merge IDs via studio
                SetMergeInfos(StudioModelName, Job);

            Stopwatch consistencyWatch = new Stopwatch();

            if (Job.Job_Error_Code != SosyncError.Cleanup.Value)
            {
                try
                {
                    SetupChildJobRequests();
                    HandleChildJobs(
                        "Child Job",
                        RequiredChildJobs,
                        Job.Children,
                        flowService,
                        null,
                        consistencyWatch,
                        ref requireRestart,
                        ref restartReason);
                }
                catch (ConsistencyException)
                {
                    requireRestart = true;
                }
                catch (Exception ex)
                {
                    throw new ChildJobException(ex.Message, ex);
                }

                if (requireRestart)
                    return;

                try
                {
                    var description = $"Merging [{Job.Sync_Target_System}] {Job.Sync_Target_Model} {Job.Sync_Target_Record_ID} into {Job.Sync_Target_Merge_Into_Record_ID}";
                    HandleTransformation(description, null, consistencyWatch, ref requireRestart, ref restartReason);
                }
                catch (Exception ex)
                {
                    throw new TransformationException(ex.Message, ex);
                }
            }
            else
            {
                Svc.Log.LogInformation($"Job error code was {SosyncError.Cleanup.Value}, only doing cleanup!");
            }

            // Job clean-up
            try
            {
                HandleChildJobs(
                    "Post Transformation Cleanup Child Job",
                    RequiredPostTransformChildJobs,
                    null,
                    flowService,
                    null,
                    consistencyWatch,
                    ref requireRestart,
                    ref restartReason);
            }
            catch (Exception ex)
            {
                throw new SyncCleanupException(ex.Message, ex);
            }

            // Done - update job success
            UpdateJobSuccess(false);
        }

        private void SetMergeInfos(string modelName, SyncJob job)
        {
            using (var db = GetDb())
            {
                if (job.Job_Source_System == SosyncSystem.FSOnline.Value)
                {
                    throw new SyncerException("Merging from 'fso' to 'fs' currently not supported.");
                }
                else
                {
                    job.Sync_Source_System = SosyncSystem.FundraisingStudio.Value;
                    job.Sync_Target_System = SosyncSystem.FSOnline.Value;

                    job.Sync_Source_Model = StudioModelName;
                    job.Sync_Target_Model = OnlineModelName;

                    var sourceStudioID = job.Job_Source_Record_ID;
                    var sourceOnlineID = GetOnlineIDFromOdooViaStudioID(modelName, sourceStudioID) ?? job.Job_Source_Target_Record_ID;

                    var mergeStudioID = job.Job_Source_Merge_Into_Record_ID;
                    var mergeOnlineID = GetOnlineIDFromOdooViaStudioID(modelName, mergeStudioID.Value) ?? job.Job_Source_Target_Merge_Into_Record_ID;

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
