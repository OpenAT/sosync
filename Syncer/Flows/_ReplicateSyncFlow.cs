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
        public ReplicateSyncFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
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

            if (!SkipAutoSyncSource)
            {
                SetSyncSource(Job, out initialWriteDate, consistencyWatch);
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

            HandleChildJobs(
                "Child Job",
                RequiredChildJobs,
                flowService,
                initialWriteDate, 
                consistencyWatch, 
                ref requireRestart,
                ref restartReason);

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

        /// <summary>
        /// Reads the write dates and foreign ids for both models (if possible)
        /// and compares the write dates to determine the sync direction.
        /// </summary>
        /// <param name="job">The job to be updated with the sync source.</param>
        private void SetSyncSource(SyncJob job, out DateTime? writeDate, Stopwatch consistencyWatch)
        {
            LogMs(0, $"\n{nameof(SetSyncSource)} start", job.Job_ID, 0);
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
                        //var isJobDateSimilar = IsJobDateSimilarToWriteDate(job, studioInfo, onlineInfo);

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
                        //var isJobDateSimilar = IsJobDateSimilarToWriteDate(job, studioInfo, onlineInfo);

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
            LogMs(0, $"{nameof(SetSyncSource)} done", Job.Job_ID, Convert.ToInt64(s.Elapsed.TotalMilliseconds));
            s.Reset();
        }

        private bool IsJobDateSimilarToWriteDate(SyncJob job, ModelInfo studioInfo, ModelInfo onlineInfo)
        {
            var tolerance = 2000;
            var modelInfo = studioInfo;

            if (job.Job_Source_System == SosyncSystem.FSOnline)
                modelInfo = onlineInfo;

            var sosyncDate = (modelInfo.SosyncWriteDate ?? modelInfo.WriteDate.Value);
            var diff = job.Job_Date - sosyncDate;

            var result = false;

            if (diff.TotalMilliseconds <= tolerance)
                result = true;

            Log.LogWarning($"{job.Job_Source_Model}\nsimilar = {result}\nJD = {job.Job_Date}\nWD = {sosyncDate}");

            return result;
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
            LogMs(1, nameof(GetModelInfosViaOnline) + "-FSO", job.Job_ID, OdooService.Client.LastRpcTime);

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
                LogMs(1, nameof(GetModelInfosViaOnline) + "-MSSQL", job.Job_ID, s.ElapsedMilliseconds);
            }
        }

        private void GetModelInfosViaStudio(SyncJob job, out ModelInfo studioInfo, out ModelInfo onlineInfo)
        {
            onlineInfo = null;

            Stopwatch s = new Stopwatch();
            s.Start();
            studioInfo = GetStudioInfo(job.Job_Source_Record_ID);
            s.Stop();
            LogMs(1, nameof(GetModelInfosViaStudio) + "-MSSQL", job.Job_ID, s.ElapsedMilliseconds);

            if (studioInfo == null)
                throw new ModelNotFoundException(
                    SosyncSystem.FundraisingStudio,
                    job.Job_Source_Model,
                    job.Job_Source_Record_ID);

            if (studioInfo.ForeignID != null)
            {
                onlineInfo = GetOnlineInfo(studioInfo.ForeignID.Value);
               LogMs(1, nameof(GetModelInfosViaStudio) + "-FSO", job.Job_ID, OdooService.Client.LastRpcTime);
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
            Action<TOdoo, TStudio> copyOdooToStudio
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

                    UpdateSyncTargetRequest(Serializer.ToXML(studioModel));

                    var studioModelID = 0;
                    try
                    {
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

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(studioModel));

                    copyOdooToStudio(onlineModel, studioModel);

                    studioModel.sosync_write_date = onlineModel.Sosync_Write_Date ?? onlineModel.Write_Date;
                    studioModel.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(studioModel));

                    try
                    {
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
