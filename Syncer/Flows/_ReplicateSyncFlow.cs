using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Data.Models;
using System.Diagnostics;
using Syncer.Exceptions;
using Syncer.Attributes;
using System.Reflection;
using WebSosync.Data;
using Microsoft.Extensions.Logging;
using WebSosync.Common;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for sync flows that replicated models.
    /// </summary>
    public abstract class ReplicateSyncFlow : SyncFlow
    {
        public ReplicateSyncFlow(IServiceProvider svc) : base(svc)
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
            UpdateJobRunCount(Job);

            CheckRunCount(5);

            DateTime? initialWriteDate = null;
            Stopwatch consistencyWatch = new Stopwatch();

            SetSyncSource(Job, out initialWriteDate, consistencyWatch);

            if (string.IsNullOrEmpty(Job.Sync_Source_System))
            {
                // Model is up to date in both systems. Close
                // the job, and stop this flow
                UpdateJobSuccess(true);
                return;
            }

            HandleChildJobs(flowService, initialWriteDate, consistencyWatch, ref requireRestart, ref restartReason);
            HandleTransformation(initialWriteDate, consistencyWatch, ref requireRestart, ref restartReason);
        }

        /// <summary>
        /// Reads the write dates and foreign ids for both models (if possible)
        /// and compares the write dates to determine the sync direction.
        /// </summary>
        /// <param name="job">The job to be updated with the sync source.</param>
        private void SetSyncSource(SyncJob job, out DateTime? writeDate, Stopwatch consistencyWatch)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            try
            {
                ModelInfo onlineInfo = null;
                ModelInfo studioInfo = null;

                // First off, get the model info from the system that
                // initiated the sync job
                if (job.Job_Source_System == SosyncSystem.FSOnline)
                    GetModelInfosViaOnline(job, out onlineInfo, out studioInfo);
                else
                    GetModelInfosViaStudio(job, out studioInfo, out onlineInfo);

                writeDate = null;

                // Get the attributes for the model names
                var studioAtt = this.GetType().GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
                var onlineAtt = this.GetType().GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();

                if (onlineInfo != null && onlineInfo.ForeignID.HasValue && !(onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate).HasValue)
                    throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fso]: sosync_fs_id={onlineInfo.ForeignID} but sosync_write_date=null and write_date=null.");

                if (studioInfo != null && studioInfo.ForeignID.HasValue && !(studioInfo.SosyncWriteDate ?? studioInfo.WriteDate).HasValue)
                    throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fs]: sosync_fso_id={onlineInfo.ForeignID} but sosync_write_date=null and write_date=null.");

                // Now update the job information depending on the available
                // model infos
                if (onlineInfo != null && studioInfo != null)
                {
                    // Both systems already have the model, check write date
                    // and decide source. If any sosync_write_date is null,
                    // use the write_date instead. If both are null, an exception
                    // is thrown to abort the synchronization

                    if (!onlineInfo.SosyncWriteDate.HasValue && !onlineInfo.WriteDate.HasValue)
                        throw new SyncerException($"Model {job.Job_Source_Model} had neither sosync_write_date nor write_date in [fso]");

                    if (!studioInfo.SosyncWriteDate.HasValue && !studioInfo.WriteDate.HasValue)
                        throw new SyncerException($"Model {job.Job_Source_Model} had neither sosync_write_date nor write_date in [fs]");

                    // XML-RPC default format for date/time does not include milliseconds, so
                    // when comparing the difference, up to 999 milliseconds difference is still
                    // considered equal
                    var toleranceMS = 0;

                    var diff = GetWriteDateDifference(job.Job_Source_Model, studioInfo, onlineInfo, toleranceMS);

                    if (diff.TotalMilliseconds == 0)
                    {
                        // Both models are within tolerance, and considered up to date.
                        writeDate = null;

                        // Log.LogInformation($"{nameof(GetWriteDateDifference)}() - {anyModelName} write diff: {SpecialFormat.FromMilliseconds((int)Math.Abs(result.TotalMilliseconds))} Tolerance: {SpecialFormat.FromMilliseconds(toleranceMS)}");
                        UpdateJobSourceAndTarget(
                            job, "", "", null, "", "", null,
                            $"Model up to date (diff: {SpecialFormat.FromMilliseconds((int)diff.TotalMilliseconds)}, tolerance: {SpecialFormat.FromMilliseconds(toleranceMS)})");
                    }
                    else if (diff.TotalMilliseconds < 0)
                    {
                        // The studio model was newer
                        writeDate = studioInfo.SosyncWriteDate ?? studioInfo.WriteDate;
                        UpdateJobSourceAndTarget(
                            job,
                            SosyncSystem.FundraisingStudio,
                            studioAtt.Name,
                            studioInfo.ID,
                            SosyncSystem.FSOnline,
                            onlineAtt.Name,
                            studioInfo.ForeignID,
                            null);
                    }
                    else
                    {
                        // The online model was newer
                        writeDate = onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate;
                        UpdateJobSourceAndTarget(
                            job,
                            SosyncSystem.FSOnline,
                            onlineAtt.Name,
                            onlineInfo.ID,
                            SosyncSystem.FundraisingStudio,
                            studioAtt.Name,
                            onlineInfo.ForeignID,
                            null);
                    }
                }
                else if (onlineInfo != null && studioInfo == null)
                {
                    // The online model is not yet in studio
                    writeDate = onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FSOnline,
                        onlineAtt.Name,
                        onlineInfo.ID,
                        SosyncSystem.FundraisingStudio,
                        studioAtt.Name,
                        null,
                        null);
                }
                else if (onlineInfo == null && studioInfo != null)
                {
                    // The studio model is not yet in online
                    writeDate = studioInfo.SosyncWriteDate ?? studioInfo.WriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FundraisingStudio,
                        studioAtt.Name,
                        studioInfo.ID,
                        SosyncSystem.FSOnline,
                        onlineAtt.Name,
                        null,
                        null);
                }
                else
                {
                    throw new SyncerException(
                        $"Invalid state, could find {nameof(ModelInfo)} for either system.");
                }
                consistencyWatch.Start();
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.SyncSource, $"2) Sync direction:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "SetSyncSource", Job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();
        }

        /// <summary>
        /// Returns the difference of two date time fields. If
        /// the difference is within a certain tolerance, the
        /// difference is returned as zero.
        /// </summary>
        /// <param name="onlineWriteDate">The FSO time stamp.</param>
        /// <param name="studioWriteDate">The Studio time stamp.</param>
        /// <returns></returns>
        private TimeSpan GetWriteDateDifference(string anyModelName, ModelInfo studioInfo, ModelInfo onlineInfo, int toleranceMS)
        {
            var onlineWriteDate = onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate.Value;
            var studioWriteDate = (studioInfo.SosyncWriteDate ?? studioInfo.WriteDate.Value.ToUniversalTime());

            var result = onlineWriteDate - studioWriteDate;

            Log.LogInformation($"job ({Job.Job_ID}): {nameof(GetWriteDateDifference)}() - {anyModelName} write diff: {SpecialFormat.FromMilliseconds((int)Math.Abs(result.TotalMilliseconds))} Tolerance: {SpecialFormat.FromMilliseconds(toleranceMS)}");

            // If the difference is within the tolerance,
            // return zero
            if (Math.Abs(result.TotalMilliseconds) <= toleranceMS)
                result = TimeSpan.FromMilliseconds(0);

            return result;
        }

        private void GetModelInfosViaOnline(SyncJob job, out ModelInfo onlineInfo, out ModelInfo studioInfo)
        {
            studioInfo = null;

            onlineInfo = GetOnlineInfo(job.Job_Source_Record_ID);
            LogMs(1, "GetOnlineInfo 1", job.Job_ID, OdooService.Client.LastRpcTime);

            if (onlineInfo == null)
                throw new ModelNotFoundException(
                    SosyncSystem.FSOnline,
                    job.Job_Source_Model,
                    job.Job_Source_Record_ID);

            if (onlineInfo.ForeignID != null)
            {
                Stopwatch s = new Stopwatch();
                s.Start();
                studioInfo = GetStudioInfo(onlineInfo.ForeignID.Value);
                s.Stop();
                LogMs(1, $"GetStudioInfo 1", job.Job_ID, s.ElapsedMilliseconds);
            }
        }

        private void GetModelInfosViaStudio(SyncJob job, out ModelInfo studioInfo, out ModelInfo onlineInfo)
        {
            onlineInfo = null;

            Stopwatch s = new Stopwatch();
            s.Start();
            studioInfo = GetStudioInfo(job.Job_Source_Record_ID);
            s.Stop();
            LogMs(1, "GetStudioInfo 2", job.Job_ID, s.ElapsedMilliseconds);

            if (studioInfo == null)
                throw new ModelNotFoundException(
                    SosyncSystem.FundraisingStudio,
                    job.Job_Source_Model,
                    job.Job_Source_Record_ID);

            if (studioInfo.ForeignID != null)
            {
                onlineInfo = GetOnlineInfo(studioInfo.ForeignID.Value);
                LogMs(1, "GetOnlineInfo 2", job.Job_ID, OdooService.Client.LastRpcTime);
            }
        }
    }
}
