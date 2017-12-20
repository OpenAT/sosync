using dadi_data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Odoo;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for any sync flow.
    /// </summary>
    public abstract class SyncFlow : IDisposable
    {
        #region Constants
        public const string MssqlTargetSuccessMessage = "success";
        #endregion

        #region Members
        private List<ChildJobRequest> _requiredChildJobs;
        private SyncJob _job;
        #endregion

        #region Properties
        protected ILogger<SyncFlow> Log { get; private set; }
        protected IServiceProvider Service { get; private set; }
        protected OdooService OdooService { get; private set; }
        protected MdbService MdbService { get; private set; }
        protected OdooFormatService OdooFormat { get; private set; }
        protected SerializationService Serializer { get; private set; }
        
        public CancellationToken CancelToken { get; set; }
        #endregion

        #region Constructors
        public SyncFlow(IServiceProvider svc)
        {
            Service = svc;
            Log = Service.GetService<ILogger<SyncFlow>>();
            OdooService = Service.GetService<OdooService>();
            MdbService = Service.GetService<MdbService>();
            OdooFormat = Service.GetService<OdooFormatService>();
            Serializer = Service.GetService<SerializationService>();

            _requiredChildJobs = new List<ChildJobRequest>();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Get IDs and write date for the model in online.
        /// </summary>
        /// <param name="onlineID">The ID for the model.</param>
        /// <returns></returns>
        protected abstract ModelInfo GetOnlineInfo(int onlineID);

        /// <summary>
        /// Get IDs and write date for the model in studio.
        /// </summary>
        /// <param name="studioID">The Studio ID for the model.</param>
        /// <returns></returns>
        protected abstract ModelInfo GetStudioInfo(int studioID);

        /// <summary>
        /// Configure the flow for the sync direction online to studio.
        /// This configuration can at some point be read from meta data
        /// from fs online.
        /// </summary>
        /// <param name="onlineID">The Online ID for the model.</param>
        protected abstract void SetupOnlineToStudioChildJobs(int onlineID);

        /// <summary>
        /// Configure the flow for the sync direction studio to online.
        /// </summary>
        /// <param name="studioID">The Studio ID for the model.</param>
        protected abstract void SetupStudioToOnlineChildJobs(int studioID);

        /// <summary>
        /// Read the studio model with the given ID and transform it
        /// to an online model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="studioID">The Studio ID for the source.</param>
        protected abstract void TransformToOnline(int studioID, TransformType action);

        /// <summary>
        /// Read the online model with the given ID and transform it
        /// to a studio model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="onlineID">The Online ID for the source.</param>
        protected abstract void TransformToStudio(int onlineID, TransformType action);

        /// <summary>
        /// Get the ID, sosync_write_date and write_date for a fso model.
        /// </summary>
        /// <param name="id">The fso ID of the model.</param>
        /// <param name="model">The model name.</param>
        /// <returns></returns>
        protected ModelInfo GetDefaultOnlineModelInfo(int id, string model)
        {
            var dicModel = OdooService.Client.GetDictionary(
                model,
                id,
                new string[] { "id", "sosync_fs_id", "write_date", "sosync_write_date" });

            if (!OdooService.Client.IsValidResult(dicModel))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, model, id);

            var fsID = OdooConvert.ToInt32((string)dicModel["sosync_fs_id"]);

            if (fsID == 0)
                fsID = null;

            var sosyncWriteDate = OdooConvert.ToDateTime((string)dicModel["sosync_write_date"], true);
            var writeDate = OdooConvert.ToDateTime((string)dicModel["write_date"], true);

            return new ModelInfo(id, fsID, sosyncWriteDate, writeDate);
        }

        protected int? GetFsoIdByFsId(string modelName, int fsId)
        {
            var odooPartnerID = OdooService.Client.SearchByField(modelName, "sosync_fs_id", "=", fsId.ToString()).SingleOrDefault();
            if (odooPartnerID > 0)
                return odooPartnerID;

            return null;
        }

        protected int? GetFsIdByFsoId(string modelName, string idName, int onlineID)
        {
            // Since we're only running a simple query, the DataService type doesn't matter
            using (var db = MdbService.GetDataService<dboPerson>())
            {
                var foundStudioID = db.ExecuteQuery<int?>(
                    $"select {idName} from {modelName} where sosync_fso_id = @fso_id",
                    new { fso_id = onlineID })
                    .SingleOrDefault();

                return foundStudioID;
            }
        }

        protected bool IsValidFsID(int? sosync_fs_id)
        {
            return sosync_fs_id.HasValue && sosync_fs_id.Value > 0;
        }

        /// <summary>
        /// Starts the data flow.
        /// </summary>
        /// <param name="_job"></param>
        public void Start(FlowService flowManager, SyncJob job, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            _job = job;

            UpdateJobRunCount(_job);

            Stopwatch s = new Stopwatch();

            // -----------------------------------------------------------------------
            // 1) First off, check run count (and eventually throw exception)
            // -----------------------------------------------------------------------
            s.Start();
            try
            {
                CheckRunCount(5);
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.RunCounter, $"1) Run counter:\n{ex.ToString()}");
                throw;
            }

            s.Stop();
            LogMs(0, "RunCount", _job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();

            // -----------------------------------------------------------------------
            // 2) Determine the sync direction and update the job
            // -----------------------------------------------------------------------
            s.Start();
            DateTime? initialWriteDate = null;
            Stopwatch consistencyWatch = new Stopwatch();
            try
            {
                SetSyncSource(_job, out initialWriteDate);
                consistencyWatch.Start();

                if (string.IsNullOrEmpty(_job.Sync_Source_System))
                {
                    // Model is up to date in both systems. Close
                    // the job, and stop this flow
                    UpdateJobSuccess(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.SyncSource, $"2) Sync direction:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "SetSyncSource", _job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();

            // -----------------------------------------------------------------------
            // 3) Now check the child jobs
            // -----------------------------------------------------------------------
            s.Start();
            try
            {
                // The derived sync flows can use RequestChildJob() method to
                // fill _requiredChildJobs.

                // This setup is not counted towards child_job_start and child_job_end.

                if (_job.Sync_Source_System == SosyncSystem.FSOnline)
                    SetupOnlineToStudioChildJobs(_job.Sync_Source_Record_ID.Value);
                else
                    SetupStudioToOnlineChildJobs(_job.Sync_Source_Record_ID.Value);

                // Process the requested child jobs:
                // 1) Ignore finished child jobs
                // 2) If child job is open --> Skipped?
                // 3) Create all requested child jobs
                try
                {
                    // try-finally to ensure the child_job_end date is written.
                    // Actual errors should still bubble up, NOT be caught here.
                    UpdateJobChildStart(_job);

                    var allChildJobsFinished = true;

                    foreach (ChildJobRequest request in _requiredChildJobs)
                    {
                        if (!IsConsistent(_job, initialWriteDate, consistencyWatch))
                        {
                            // Job is inconsistent, cancel the flow and leave it "in progress",
                            // so it will be restarted later.
                            UpdateJobInconsistent(_job, 1);
                            requireRestart = true;
                            restartReason = "Consistency check 1";
                            return;
                        }

#warning TODO: bubble up the hierarchy to check for circular references
                        // 1) Check if any parent job already has
                        //    sync_source_system, sync_source_model, sync_source_record-id

                        // 2 Create & Process child job
                        var childJob = new SyncJob()
                        {
                            Parent_Job_ID = _job.Job_ID,
                            Job_State = SosyncState.New,
                            Job_Date = DateTime.UtcNow,
                            Job_Fetched = DateTime.UtcNow,
                            Job_Source_System = request.JobSourceSystem,
                            Job_Source_Model = request.JobSourceModel,
                            Job_Source_Record_ID = request.JobSourceRecordID,
                            Job_Last_Change = DateTime.UtcNow
                        };

                        // Try to fetch a child job with the same main properties
                        SyncJob entry = null;
                        using (var db = Service.GetService<DataService>())
                        {
                            entry = db.GetJobBy(_job.Job_ID, childJob.Job_Source_System, childJob.Job_Source_Model, childJob.Job_Source_Record_ID);
                            
                            // If the child job wasn't created yet, create it
                            if (entry == null)
                            {
                                Log.LogInformation($"Creating child job for job ({_job.Job_ID}) for [{childJob.Job_Source_System}] {childJob.Job_Source_Model} ({childJob.Job_Source_Record_ID}).");
                                db.CreateJob(childJob);
                                _job.Children.Add(childJob);

                                entry = childJob;
                            }
                        }

                        if (entry.Job_State == SosyncState.Error)
                            throw new SyncerException($"Child job ({entry.Job_ID}) for [{entry.Job_Source_System}] {entry.Job_Source_Model} ({entry.Job_Source_Record_ID}) failed.");

                        // If the job is new or marked as "in progress", run it
                       if (entry.Job_State == SosyncState.New || entry.Job_State == SosyncState.InProgress)
                        { 
                            Log.LogDebug($"Executing child job ({entry.Job_ID})");

                            UpdateJobStart(entry, DateTime.UtcNow);

                            // Get the flow for the job source model, and start it
                            SyncFlow flow = (SyncFlow)Service.GetService(flowManager.GetFlow(entry.Job_Source_Model));
                            flow.Start(flowManager, entry, DateTime.UtcNow, ref requireRestart, ref restartReason);

                            // Be sure to use logic & operator
                            if (entry.Job_State == SosyncState.Done)
                                allChildJobsFinished &= true;
                            else
                                allChildJobsFinished &= false;
                        }
                    }

                    //if (!allChildJobsFinished)
                    //    throw new SyncerException($"No child job errors occured, but there were still unfinished child jobs left.");

                    if (!allChildJobsFinished)
                    {
                        // Child jobs are not yet finished, despite just processing them.
                        // Require restart, and stop this job, but leave it open so it is
                        // processed again
                        UpdateJobInconsistent(_job, 2);
                        requireRestart = true;
                        restartReason = "Consistency check 2";
                        return;
                    }
                }
                finally
                {
                    // In any case, log the child end at this point
                    UpdateJobChildEnd();
                }
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.ChildJob, $"3) Child jobs:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "ChildJobs", _job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();

            // -----------------------------------------------------------------------
            // 4) Transformation
            // -----------------------------------------------------------------------
            s.Start();
            try
            {
                if (!IsConsistent(_job, initialWriteDate, consistencyWatch))
                {
                    // Job is inconsistent, cancel the current flow. Make sure
                    // To do this outside the try-finally block, because we want
                    // the job to stay "in progress".
                    UpdateJobInconsistent(_job, 3);
                    requireRestart = true;
                    restartReason = "Consistency check 3";
                    return;
                }

                try
                {
                    // try-finally to ensure the sync_end date is written.
                    // Actual errors should still bubble up, NOT be caught here.

                    UpdateJobSyncStart();

                    var action = _job.Sync_Target_Record_ID > 0 ? TransformType.Update : TransformType.CreateNew;

                    var targetIdText = _job.Sync_Target_Record_ID.HasValue ? _job.Sync_Target_Record_ID.Value.ToString() : "new";
                    Log.LogInformation($"Transforming [{_job.Sync_Source_System}] {_job.Sync_Source_Model} ({_job.Sync_Source_Record_ID}) to [{_job.Sync_Target_System}] {_job.Sync_Target_Model} ({targetIdText})");

                    if (_job.Sync_Source_System == SosyncSystem.FSOnline)
                        TransformToStudio(_job.Sync_Source_Record_ID.Value, action);
                    else
                        TransformToOnline(_job.Sync_Source_Record_ID.Value, action);
                }
                finally
                {
                    // In any case, log the transformation/sync end at this point
                    UpdateJobSyncEnd();
                }

                // Done - update job success
                UpdateJobSuccess(false);
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.Transformation, $"4) Transformation/sync:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "Transformation", _job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();
        }

        /// <summary>
        /// Inform the sync flow that a specific model is needed as a child job
        /// for the current flow.
        /// </summary>
        /// <param name="system">The job source system, use <see cref="SosyncSystem"/> constants.</param>
        /// <param name="model">The requested source model.</param>
        /// <param name="id">The requested source ID for the model.</param>
        protected void RequestChildJob(string system, string model, int id)
        {
            _requiredChildJobs.Add(new ChildJobRequest(system, model, id));
        }

        /// <summary>
        /// Checks the current run_count value of the job against the
        /// specified maximum value. Throws an <see cref="RunCountException"/>
        /// if the maximum value is reached or exceeded.
        /// </summary>
        /// <param name="maxRuns">The number of runs upon an exception is raised.</param>
        private void CheckRunCount(int maxRuns)
        {
            if (_job.Job_Run_Count >= maxRuns)
                throw new RunCountException(_job.Job_Run_Count, maxRuns);
        }

        /// <summary>
        /// Reads the write dates and foreign ids for both models (if possible)
        /// and compares the write dates to determine the sync direction.
        /// </summary>
        /// <param name="job">The job to be updated with the sync source.</param>
        private void SetSyncSource(SyncJob job, out DateTime? writeDate)
        {
            ModelInfo onlineInfo = null;
            ModelInfo studioInfo = null;

            // First off, get the model info from the system that
            // initiated the sync job
            if (job.Job_Source_System == SosyncSystem.FSOnline)
            {
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
            else
            {
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
                else if(diff.TotalMilliseconds < 0)
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
        }

        /// <summary>
        /// Checks if the specified write date is still the same in the source system.
        /// </summary>
        /// <param name="job">The job to be checked.</param>
        /// <param name="sosyncWriteDate">The write date of the job since the last read.</param>
        /// <returns></returns>
        private bool IsConsistent(SyncJob job, DateTime? sosyncWriteDate, Stopwatch consistencyWatch)
        {
            var maxMS = 250;

            if (consistencyWatch.ElapsedMilliseconds < maxMS)
            {
                Log.LogInformation($"Job ({job.Job_ID}) Skipping consistency check, last check under {maxMS}ms.");
                return true;
            }

            Log.LogInformation($"Job ({job.Job_ID}) Checking model consistency");

            ModelInfo currentInfo = null;

            if (job.Sync_Source_System == SosyncSystem.FSOnline)
                currentInfo = GetOnlineInfo(job.Sync_Source_Record_ID.Value);
            else
                currentInfo = GetStudioInfo(job.Sync_Source_Record_ID.Value);

            consistencyWatch.Stop();
            consistencyWatch.Reset();
            consistencyWatch.Start();

            // Do not use any tolerance here. It's a check to see if the provided write date
            // is different from what is currently in the model
            if (sosyncWriteDate.HasValue && sosyncWriteDate.Value == (currentInfo.SosyncWriteDate ?? currentInfo.WriteDate).Value)
                return true;

            return false;
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

            Log.LogInformation($"job ({_job.Job_ID}): {nameof(GetWriteDateDifference)}() - {anyModelName} write diff: {SpecialFormat.FromMilliseconds((int)Math.Abs(result.TotalMilliseconds))} Tolerance: {SpecialFormat.FromMilliseconds(toleranceMS)}");

            // If the difference is within the tolerance,
            // return zero
            if (Math.Abs(result.TotalMilliseconds) <= toleranceMS)
                result = TimeSpan.FromMilliseconds(0);

            return result;
        }

        protected void UpdateSyncTargetDataBeforeUpdate(string data)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Data_Before_Update");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Target_Data_Before = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncTargetDataBeforeUpdate), db, _job);
            }
        }

        protected void UpdateSyncSourceData(string data)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Source_Data");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Source_Data = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncSourceData), db, _job);
            }
        }

        protected void UpdateSyncTargetRequest(string requestData)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Request");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Target_Request = requestData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncTargetRequest), db, _job);
            }
        }

        protected void UpdateSyncTargetAnswer(string answerData, int? createdID)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Answer");

            using (var db = Service.GetService<DataService>())
            {
                if (createdID.HasValue)
                    _job.Sync_Target_Record_ID = createdID.Value;

                _job.Sync_Target_Answer = answerData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncTargetAnswer), db, _job);
            }
        }

        /// <summary>
        /// Updates the sync source data and log field.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        /// <param name="srcSystem">The sync source system.</param>
        /// <param name="srcModel">The sync source model.</param>
        /// <param name="srcId">The sync source ID.</param>
        /// <param name="log">Information to be logged.</param>
        private void UpdateJobSourceAndTarget(SyncJob job, string srcSystem, string srcModel, int? srcId, string targetSystem, string targetModel, int? targetId, string log)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: check source");

            using (var db = Service.GetService<DataService>())
            {
                job.Sync_Source_System = srcSystem;
                job.Sync_Source_Model = srcModel;
                job.Sync_Source_Record_ID = srcId;

                job.Sync_Target_System = targetSystem;
                job.Sync_Target_Model = targetModel;
                job.Sync_Target_Record_ID = targetId;

                job.Job_Log = log ?? job.Job_Log; // If log is null, keep whatever is already in Job_Log
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateJobSourceAndTarget), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing started.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        private void UpdateJobChildStart(SyncJob job)
        {
            Log.LogDebug($"Updating job { job.Job_ID}: child start");

            using (var db = Service.GetService<DataService>())
            {
                job.Child_Job_Start = DateTime.UtcNow;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobChildStart), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing finished.
        /// </summary>
        private void UpdateJobChildEnd()
        {
            Log.LogDebug($"Updating job { _job.Job_ID}: child end");

            using (var db = Service.GetService<DataService>())
            {
                _job.Child_Job_End = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobChildEnd), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that the tansformation started.
        /// </summary>
        private void UpdateJobSyncStart()
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: transformation/sync start");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Start = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSyncStart), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that the transformation finished.
        /// </summary>
        private void UpdateJobSyncEnd()
        {
            Log.LogDebug($"Updating job { _job.Job_ID}: transformation/sync end");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_End = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSyncEnd), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating processing started.
        /// </summary>
        private void UpdateJobStart(SyncJob job, DateTime loadTimeUTC)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: job start");

            using (var db = Service.GetService<DataService>())
            {
                job.Job_State = SosyncState.InProgress;
                job.Job_Start = loadTimeUTC;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobStart), db, _job);
            }
        }

        private void UpdateJobRunCount(SyncJob job)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: run_count");

            using (var db = Service.GetService<DataService>())
            {
                job.Job_Run_Count += 1;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobRunCount), db, _job);
            }
        }

        private void UpdateJobInconsistent(SyncJob job, int nr)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: job_log");

            using (var db = Service.GetService<DataService>())
            {
                job.Job_Log = $"Consistency check {nr} failed, exiting job, leaving it in progress.";
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobInconsistent), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating the processing was
        /// done successfully.
        /// </summary>
        /// <param name="wasInSync">Set to true, if the job was
        /// finished because it was already in sync.</param>
        private void UpdateJobSuccess(bool wasInSync)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: job done {(wasInSync ? " (model already in sync)" : "")}");

            using (var db = Service.GetService<DataService>())
            {
                _job.Job_State = SosyncState.Done;
                _job.Job_End = DateTime.Now.ToUniversalTime();
                _job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSuccess), db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating an an error occured while processing.
        /// </summary>
        /// <param name="errorCode">Use one of the values from <see cref="SosyncError"/>.</param>
        /// <param name="errorText">The custom error text.</param>
        private void UpdateJobError(string errorCode, string errorText)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: job error");

            using (var db = Service.GetService<DataService>())
            {
                _job.Job_State = SosyncState.Error;
                _job.Job_End = DateTime.UtcNow;
                _job.Job_Error_Code = errorCode;
                _job.Job_Error_Text = errorText;
                _job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobError), db, _job);
            }
        }

        private void UpdateJob(string method, DataService db, SyncJob job)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            db.UpdateJob(job);

            s.Stop();
            LogMs(1, method, job.Job_ID, s.ElapsedMilliseconds);
        }

        protected void LogMs(int lvl, string name, int? jobId, long ms)
        {
            Log.LogDebug($"Job {jobId ?? _job.Job_ID}: {name} elapsed: {ms} ms");
        }

        public void Dispose()
        {

        }
        #endregion
    }
}
