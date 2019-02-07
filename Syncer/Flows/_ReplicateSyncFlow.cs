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
using dadi_data.Models;
using DaDi.Odoo.Models;
using dadi_data.Interfaces;
using System.Linq;
using DaDi.Odoo;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for sync flows that replicated models.
    /// </summary>
    public abstract class ReplicateSyncFlow : SyncFlow
    {
        #region Fields
        private IServiceProvider svc;
        #endregion

        #region Constructors
        public ReplicateSyncFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }
        #endregion

        #region Methods
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

            DateTime? initialWriteDate = null;
            Stopwatch consistencyWatch = new Stopwatch();

            ModelInfo onlineInfo = null;
            ModelInfo studioInfo = null;

            GetInfos(Job, out studioInfo, out onlineInfo);

            if (!SkipAutoSyncSource)
            {
                SetSyncSource(Job, out initialWriteDate, consistencyWatch, studioInfo, onlineInfo);
            }
            else
            {
                // If automatic sync source detection is disabled,
                // just fetch the initial write dates depending on
                // the job values
                if (Job.Job_Source_System == SosyncSystem.FundraisingStudio)
                {
                    var info = GetStudioInfo(Job.Job_Source_Record_ID);
                    initialWriteDate = info.SosyncWriteDate ?? info.WriteDate;
                }
                else
                {
                    var info = GetOnlineInfo(Job.Job_Source_Record_ID);
                    initialWriteDate = info.SosyncWriteDate ?? info.WriteDate;
                }
            }

            if (string.IsNullOrEmpty(Job.Sync_Source_System))
            {
                // Model is up to date in both systems. Close
                // the job, and stop this flow
                UpdateJobSuccess(true);
                return;
            }

            // Model locking via hash
            var fsID = (Job.Sync_Source_Model == StudioModelName ? Job.Sync_Source_Record_ID : Job.Sync_Target_Record_ID) ?? 0;
            var fsoID = (Job.Sync_Source_Model == OnlineModelName ? Job.Sync_Source_Record_ID : Job.Sync_Target_Record_ID) ?? 0;

            string hash = $"{OnlineModelName}_F{fsID}_O{fsoID}";

            var locked = true;
            var lockT1 = DateTime.Now;
            var timeoutMS = Config.Model_Lock_Timeout;
            while (locked)
            {
                lock (ThreadService.JobLocks)
                {
                    if (ThreadService.JobLocks.ContainsKey(hash))
                    {
                        if (ThreadService.JobLocks[hash] > 0)
                        {
                            locked = false;
                            UpdateJobSuccessOtherThread(ThreadService.JobLocks[hash]);
                            return;
                        }
                        else
                        {
                            locked = true;
                        }
                    }
                    else
                    {
                        // No entry = free, so lock it
                        ThreadService.JobLocks.Add(hash, 0);
                        locked = false;
                    }
                }

                if (locked)
                {
                    Log.LogInformation($"Job {Job.ID} locked by hash {hash}");
                    System.Threading.Thread.Sleep(100);

                    if ((DateTime.Now - lockT1).TotalMilliseconds > timeoutMS)
                        throw new SyncerException($"Lock for hash {hash} timed out.");
                }
            }

            try
            {
                SetupChildJobRequests();
                HandleChildJobs(
                    "Child Job",
                    RequiredChildJobs,
                    flowService,
                    initialWriteDate,
                    consistencyWatch,
                    ref requireRestart,
                    ref restartReason);

                // Matching ------------------
                LogMs(0, $"\nMatch start", Job.ID, 0);
                var matchWatch = Stopwatch.StartNew();
                if (MatchOccured(studioInfo, onlineInfo))
                {
                    // If matching was successful, reload write dates
                    // and re-determine the sync direction
                    GetInfos(Job, out studioInfo, out onlineInfo);
                    consistencyWatch.Reset();
                    SetSyncSource(Job, out initialWriteDate, consistencyWatch, studioInfo, onlineInfo);
                }
                matchWatch.Stop();
                LogMs(0, $"\nMatch end", Job.ID, (long)matchWatch.Elapsed.TotalMilliseconds);
                // ------------------

                var targetIdText = Job.Sync_Target_Record_ID.HasValue ? Job.Sync_Target_Record_ID.Value.ToString() : "new";
                var description = $"Transforming [{Job.Sync_Source_System}] {Job.Sync_Source_Model} ({Job.Sync_Source_Record_ID}) to [{Job.Sync_Target_System}] {Job.Sync_Target_Model} ({targetIdText})";
                HandleTransformation(description, initialWriteDate, consistencyWatch, ref requireRestart, ref restartReason);

                HandleChildJobs(
                    "Post Transformation Child Job",
                    RequiredPostTransformChildJobs,
                    flowService,
                    initialWriteDate,
                    consistencyWatch,
                    ref requireRestart,
                    ref restartReason);
            }
            finally
            {
                lock (ThreadService.JobLocks)
                {
                    if (Job.Job_State == SosyncState.Done)
                    {
                        // Set the job_id for the hash, indicating success
                        ThreadService.JobLocks[hash] = Job.ID;
                    }
                    else
                    {
                        // Remove the lock, so other threads can retry
                        ThreadService.JobLocks.Remove(hash);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the write dates and foreign ids for both models (if possible)
        /// and compares the write dates to determine the sync direction.
        /// </summary>
        /// <param name="job">The job to be updated with the sync source.</param>
        private void SetSyncSource(
            SyncJob job,
            out DateTime? writeDate,
            Stopwatch consistencyWatch,
            ModelInfo studioInfo,
            ModelInfo onlineInfo)
        {
            LogMs(0, $"\n{nameof(SetSyncSource)} start", job.ID, 0);
            Stopwatch s = new Stopwatch();
            s.Start();
            try
            {
                ThrowOnInvalidState(job, studioInfo, onlineInfo);

                writeDate = null;

                // Now update the job information depending on the available
                // model infos
                if (IsModelInBothSystems(studioInfo, onlineInfo))
                {
                    // Model is in both systems
                    SyncSourceViaModels(
                        studioInfo,
                        onlineInfo,
                        job,
                        ref writeDate);
                }
                else if (IsModelInOnlineOnly(studioInfo, onlineInfo))
                {
                    // The online model is not yet in studio
                    writeDate = onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FSOnline,
                        OnlineModelName,
                        onlineInfo.ID,
                        SosyncSystem.FundraisingStudio,
                        StudioModelName,
                        null,
                        null);
                }
                else if (IsModelInStudioOnly(studioInfo, onlineInfo))
                {
                    // The studio model is not yet in online
                    writeDate = studioInfo.SosyncWriteDate ?? studioInfo.WriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FundraisingStudio,
                        StudioModelName,
                        studioInfo.ID,
                        SosyncSystem.FSOnline,
                        OnlineModelName,
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
            LogMs(0, $"{nameof(SetSyncSource)} done", Job.ID, Convert.ToInt64(s.Elapsed.TotalMilliseconds));
            s.Reset();
        }

        private bool IsModelInBothSystems(ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            return (onlineInfo != null && studioInfo != null);
        }

        private bool IsModelInOnlineOnly(ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            return (onlineInfo != null && studioInfo == null);
        }

        private bool IsModelInStudioOnly(ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            return (onlineInfo == null && studioInfo != null);
        }

        private bool MatchOccured(ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            int? matchedID = null;

            int studioID = 0;
            int onlineID = 0;

            // Determine the IDs for both systems via matching
            if (IsModelInOnlineOnly(studioInfo, onlineInfo))
            {
                Log.LogInformation($"Trying to match {OnlineModelName} ({onlineInfo.ID}) in {SosyncSystem.FundraisingStudio}");
                var studioWatch = Stopwatch.StartNew();
                matchedID = MatchInStudioViaData(onlineInfo.ID);
                studioWatch.Stop();
                LogMs(0, $"{nameof(MatchInStudioViaData)}", Job.ID, (long)studioWatch.Elapsed.TotalMilliseconds);

                if (matchedID != null)
                {
                    onlineID = onlineInfo.ID;
                    studioID = matchedID.Value;
                }
            }
            else if (IsModelInStudioOnly(studioInfo, onlineInfo))
            {
                Log.LogInformation($"Trying to match {StudioModelName} ({studioInfo.ID}) in {SosyncSystem.FSOnline}");
                var onlineWatch = Stopwatch.StartNew();
                matchedID = MatchInOnlineViaData(studioInfo.ID);
                onlineWatch.Stop();
                LogMs(0, $"{nameof(MatchInOnlineViaData)}", Job.ID, (long)onlineWatch.Elapsed.TotalMilliseconds);

                if (matchedID != null)
                {
                    studioID = studioInfo.ID;
                    onlineID = matchedID.Value;
                }
            }

            // If both IDs are known, update the foreign ID in both
            // systems accordingly
            if (studioID > 0 && onlineID > 0)
            {
                Log.LogInformation($"Updating {OnlineModelName} {onlineID} setting sosync_fs_id = {studioID}");
                var onlineWatch = Stopwatch.StartNew();
                OdooService.Client.UpdateModel(OnlineModelName, new { sosync_fs_id = studioID }, onlineID);
                onlineWatch.Stop();
                LogMs(0, $"Update matched ID in {SosyncSystem.FSOnline}", Job.ID, (long)onlineWatch.Elapsed.TotalMilliseconds);

                Log.LogInformation($"Updating {StudioModelName} {studioID} setting sosync_fso_id = {onlineID}");
                var studioWatch = Stopwatch.StartNew();
                using (var db = MdbService.GetDataService<dboTypen>())
                {
                    db.ExecuteNonQuery(
                        $"UPDATE {StudioModelName} SET sosync_fso_id = @sosync_fso_id, noSyncJobSwitch = 1 " + 
                        $"WHERE {MdbService.GetStudioModelIdentity(StudioModelName)} = @id",
                        new { sosync_fso_id = onlineID, id = studioID });
                }
                studioWatch.Stop();
                LogMs(0, $"Update matched ID in {SosyncSystem.FundraisingStudio}", Job.ID, (long)studioWatch.Elapsed.TotalMilliseconds);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Search for the specified studio model in FS-Online. This happens
        /// after Child Jobs have been processed.
        /// </summary>
        /// <param name="studioID">Studio identity to look for</param>
        /// <returns>The identity in FS-Online.</returns>
        protected virtual int? MatchInOnlineViaData(int studioID)
        {
            return null;
        }

        /// <summary>
        /// Search for the specified online model in FS. This happens
        /// after Child Jobs have been processed.
        /// </summary>
        /// <param name="onlineID">The online identity to look for</param>
        /// <returns>The identity in FS.</returns>
        protected virtual int? MatchInStudioViaData(int onlineID)
        {
            return null;
        }

        private void GetInfos(SyncJob job, out ModelInfo studioInfo, out ModelInfo onlineInfo)
        {
            // First off, get the model info from the system that
            // initiated the sync job
            if (job.Job_Source_System == SosyncSystem.FSOnline)
                GetModelInfosViaOnline(job, out onlineInfo, out studioInfo);
            else
                GetModelInfosViaStudio(job, out studioInfo, out onlineInfo);
        }

        private void ThrowOnInvalidState(SyncJob job, ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            if (onlineInfo != null && onlineInfo.ForeignID.HasValue && !(onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate).HasValue)
                throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fso]: sosync_fs_id={onlineInfo.ForeignID} but sosync_write_date=null and write_date=null.");

            if (studioInfo != null && studioInfo.ForeignID.HasValue && !(studioInfo.SosyncWriteDate ?? studioInfo.WriteDate).HasValue)
                throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fs]: sosync_fso_id={onlineInfo.ForeignID} but sosync_write_date=null and write_date=null.");
        }

        private void SyncSourceViaModels(
            ModelInfo studioInfo,
            ModelInfo onlineInfo,
            SyncJob job,
            ref DateTime? writeDate)
        {
            // Both systems already have the model, check write date
            // and decide source. If any sosync_write_date is null,
            // use the write_date instead. If both are null, an exception
            // is thrown to abort the synchronization

            if (!onlineInfo.SosyncWriteDate.HasValue && !onlineInfo.WriteDate.HasValue)
                throw new SyncerException($"Model {job.Job_Source_Model} had neither sosync_write_date nor write_date in [fso]");

            if (!studioInfo.SosyncWriteDate.HasValue && !studioInfo.WriteDate.HasValue)
                throw new SyncerException($"Model {job.Job_Source_Model} had neither sosync_write_date nor write_date in [fs]");

            // job_date check: if the job_date differs, either:
            //  - Use job_date as sosync_write_date in the current flow is for a multi model
            //  - Throw and exception, if it is not a multi model

            var isJobDateSimilar = IsJobDateSimilarToWriteDate(
                job, 
                studioInfo, 
                onlineInfo,
                out var usedModelInfo);

            if (!isJobDateSimilar && IsStudioMultiModel)
            {
                // For studio multi models, substitute
                // job_date for the studio sosync_write_date
                studioInfo.SosyncWriteDate = job.Job_Date;
            }

            if (!job.Parent_Job_ID.HasValue && !isJobDateSimilar && !IsStudioMultiModel)
            {
                // For normal main jobs (not child jobs!) this is an invalid state
                throw new SyncerException(
                    $"Job {job.ID}: job_date ({job.Job_Date.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}) differs from " + 
                    $"sosync_write_date ({(usedModelInfo.SosyncWriteDate ?? usedModelInfo.WriteDate).Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}" + 
                    $", taken from {job.Job_Source_System}), aborting synchronization.");
            }

            // Figure out sync direction
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
                    StudioModelName,
                    studioInfo.ID,
                    SosyncSystem.FSOnline,
                    OnlineModelName,
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
                    OnlineModelName,
                    onlineInfo.ID,
                    SosyncSystem.FundraisingStudio,
                    StudioModelName,
                    onlineInfo.ForeignID,
                    null);
            }
        }

        private bool IsJobDateSimilarToWriteDate(SyncJob job, ModelInfo studioInfo, ModelInfo onlineInfo, out ModelInfo usedModelInfo)
        {
            var toleranceMS = 60000; // 1 Minute tolerance
            usedModelInfo = studioInfo;

            if (job.Job_Source_System == SosyncSystem.FSOnline)
                usedModelInfo = onlineInfo;

            var sosyncDate = (usedModelInfo.SosyncWriteDate ?? usedModelInfo.WriteDate.Value);
            var diff = job.Job_Date - sosyncDate;

            var isSimilar = false;

            if (diff.TotalMilliseconds > -999 && diff.TotalMilliseconds <= toleranceMS)
                isSimilar = true;

            var format = "yyyy-MM-dd HH:mm:ss.fffffff";

            var logMsg = string.Format(
                "Job: {0}: JobSourceModel = {1} similar = {2} (tolerance: {3}ms) job_date = {4} write_date = {5}",
                job.ID,
                job.Job_Source_Model,
                isSimilar,
                toleranceMS,
                job.Job_Date.ToString(format),
                sosyncDate.ToString(format));

            if (isSimilar)
                Log.LogInformation(logMsg);
            //else
            //    Log.LogWarning(logMsg);

            return isSimilar;
        }

        /// <summary>
        /// Returns the difference of two date time fields. If
        /// the difference is within a certain tolerance, the
        /// difference is returned as zero.
        /// </summary>
        /// <param name="onlineWriteDate">The FSO time stamp.</param>
        /// <param name="studioWriteDate">The Studio time stamp.</param>
        /// <returns></returns>
        private TimeSpan GetWriteDateDifference(
            string anyModelName,
            ModelInfo studioInfo,
            ModelInfo onlineInfo,
            int toleranceMS)
        {
            var onlineWriteDate = onlineInfo.SosyncWriteDate ?? onlineInfo.WriteDate.Value;
            var studioWriteDate = studioInfo.SosyncWriteDate ?? studioInfo.WriteDate.Value;

            var result = onlineWriteDate - studioWriteDate;

            Log.LogInformation($"job ({Job.ID}): {nameof(GetWriteDateDifference)}() - {anyModelName} write diff: {SpecialFormat.FromMilliseconds((int)Math.Abs(result.TotalMilliseconds))} Tolerance: {SpecialFormat.FromMilliseconds(toleranceMS)}");

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
            LogMs(1, nameof(GetModelInfosViaOnline) + "-FSO", job.ID, OdooService.Client.LastRpcTime);

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
                LogMs(1, nameof(GetModelInfosViaOnline) + "-MSSQL", job.ID, s.ElapsedMilliseconds);
            }
        }

        private void GetModelInfosViaStudio(SyncJob job, out ModelInfo studioInfo, out ModelInfo onlineInfo)
        {
            onlineInfo = null;

            Stopwatch s = new Stopwatch();
            s.Start();
            studioInfo = GetStudioInfo(job.Job_Source_Record_ID);
            s.Stop();
            LogMs(1, nameof(GetModelInfosViaStudio) + "-MSSQL", job.ID, s.ElapsedMilliseconds);

            if (studioInfo == null)
                throw new ModelNotFoundException(
                    SosyncSystem.FundraisingStudio,
                    job.Job_Source_Model,
                    job.Job_Source_Record_ID);

            if (studioInfo.ForeignID != null)
            {
                onlineInfo = GetOnlineInfo(studioInfo.ForeignID.Value);
               LogMs(1, nameof(GetModelInfosViaStudio) + "-FSO", job.ID, OdooService.Client.LastRpcTime);
            }
        }

        protected void SimpleTransformToOnline<TStudio, TOdoo>(
            int studioID,
            TransformType action,
            Func<TStudio, int> getStudioIdentity,
            Action<TStudio, Dictionary<string, object>> copyStudioToDictionary
            )
            where TStudio : MdbModelBase, ISosyncable, new()
            where TOdoo : SosyncModelBase
        {
            TStudio studioModel = null;
            using (var db = MdbService.GetDataService<TStudio>())
            {
                // studioModel = db.Read(new { zGruppeID = studioID }).SingleOrDefault();
                studioModel = db.Read(
                    $"select * from {MdbService.GetStudioModelReadView(StudioModelName)} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @ID",
                    new { ID = studioID })
                    .SingleOrDefault();

                if (!studioModel.sosync_fso_id.HasValue)
                    studioModel.sosync_fso_id = GetOnlineIDFromOdooViaStudioID(OnlineModelName, getStudioIdentity(studioModel));

                UpdateSyncSourceData(Serializer.ToXML(studioModel));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>();

                copyStudioToDictionary(studioModel, data);
                data.Add("sosync_write_date", (studioModel.sosync_write_date ?? studioModel.write_date.ToUniversalTime()));

                if (action == TransformType.CreateNew)
                {
                    data.Add("sosync_fs_id", getStudioIdentity(studioModel));
                    int odooModelID = 0;
                    try
                    {
                        odooModelID = OdooService.Client.CreateModel(OnlineModelName, data, false);
                        studioModel.sosync_fso_id = odooModelID;
                        db.Update(studioModel);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooModelID);
                    }
                }
                else
                {
                    var odooModel = OdooService.Client.GetModel<TOdoo>(OnlineModelName, studioModel.sosync_fso_id.Value);
                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

                    try
                    {
                        OdooService.Client.UpdateModel(OnlineModelName, data, studioModel.sosync_fso_id.Value, false);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
                    }
                }
            }
        }

        protected void SimpleTransformToStudio<TOdoo, TStudio>(
            int onlineID,
            TransformType action,
            Func<TStudio, int> getStudioIdentity,
            Action<TOdoo, TStudio> copyOdooToStudio,
            dboAktion parentAction = null,
            Action<dboAktion, TStudio> applyIdentity = null
            )
            where TOdoo : SosyncModelBase
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            var onlineModel = OdooService.Client.GetModel<TOdoo>(OnlineModelName, onlineID);

            if (!IsValidFsID(onlineModel.Sosync_FS_ID))
                onlineModel.Sosync_FS_ID = GetStudioIDFromMssqlViaOnlineID(
                    StudioModelName,
                    MdbService.GetStudioModelIdentity(StudioModelName),
                    onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            // List all dboAktion... Types, needed for log serialization
            var actionTypes = Assembly
                .GetAssembly(typeof(MdbModelBase))
                .GetTypes()
                .Where(x => x.Name.StartsWith("dboAktion"))
                .ToArray();

            using (var db = MdbService.GetDataService<TStudio>())
            {
                if (action == TransformType.CreateNew)
                {
                    var studioModel = new TStudio()
                    {
                        sosync_write_date = (onlineModel.Sosync_Write_Date ?? onlineModel.Write_Date).Value,
                        sosync_fso_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    copyOdooToStudio(onlineModel, studioModel);

                    // If the model is an action, protocol both the Aktion and the Aktion*-Table
                    // Without an action, just protocol the model itself
                    if (parentAction != null)
                        UpdateSyncTargetRequest(Serializer.ToXML(
                            new StudioAktion { Aktion = parentAction, AktionDetail = studioModel },
                            actionTypes));
                    else
                        UpdateSyncTargetRequest(Serializer.ToXML(studioModel));
                    // ---

                    var studioModelID = 0;
                    try
                    {
                        // If an action was specified, save that action first,
                        // then apply the identity to the studio model
                        // (studio model is an Aktion* Model in that case)
                        if (parentAction != null)
                        {
                            using (var dbAk = MdbService.GetDataService<dboAktion>())
                            {
                                dbAk.Create(parentAction);
                                applyIdentity(parentAction, studioModel);
                            }
                        }

                        db.Create(studioModel);
                        studioModelID = getStudioIdentity(studioModel);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, studioModelID);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), studioModelID);
                        throw;
                    }

                    OdooService.Client.UpdateModel(
                        OnlineModelName,
                        new { sosync_fs_id = getStudioIdentity(studioModel) },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = onlineModel.Sosync_FS_ID;
                    var studioModel = db.Read(
                        $"select * from {StudioModelName} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @ID",
                        new { ID = sosync_fs_id })
                        .SingleOrDefault();

                    // If the model is an action, protocol both the Aktion and the Aktion*-Table
                    // Without an action, just protocol the model itself
                    if (parentAction != null)
                        UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(
                            new StudioAktion { Aktion = parentAction, AktionDetail = studioModel },
                            actionTypes));
                    else
                        UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(studioModel));
                    // ---

                    copyOdooToStudio(onlineModel, studioModel);

                    studioModel.sosync_write_date = onlineModel.Sosync_Write_Date ?? onlineModel.Write_Date;
                    studioModel.noSyncJobSwitch = true;

                    // If the model is an action, protocol both the Aktion and the Aktion*-Table
                    // Without an action, just protocol the model itself
                    if (parentAction != null)
                        UpdateSyncTargetRequest(Serializer.ToXML(
                            new StudioAktion { Aktion = parentAction, AktionDetail = studioModel },
                            actionTypes));
                    else
                        UpdateSyncTargetRequest(Serializer.ToXML(studioModel));
                    // ---

                    try
                    {
                        // If an action was specified, save that action first
                        if (parentAction != null)
                        {
                            using (var dbAk = MdbService.GetDataService<dboAktion>())
                            {
                                dbAk.Update(parentAction);
                            }
                        }

                        db.Update(studioModel);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), null);
                        throw;
                    }
                }
            }
        }
        #endregion
    }
}
