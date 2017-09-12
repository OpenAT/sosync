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
        private DataService _db;
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
            _db = Service.GetService<DataService>();
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
            var sosyncWriteDate = OdooConvert.ToDateTime((string)dicModel["sosync_write_date"], true);
            var writeDate = OdooConvert.ToDateTime((string)dicModel["write_date"], true);

            return new ModelInfo(id, fsID, sosyncWriteDate, writeDate);
        }

        /// <summary>
        /// Starts the data flow.
        /// </summary>
        /// <param name="_job"></param>
        public void Start(FlowService flowManager, SyncJob job, DateTime loadTimeUTC, ref bool requireRestart)
        {
            _job = job;

            UpdateJobRunCount(_job);

            // -----------------------------------------------------------------------
            // 1) First off, check run count (and eventually throw exception)
            // -----------------------------------------------------------------------
            try
            {
                CheckRunCount(5);
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.RunCounter, $"1) Run counter:\n{ex.ToString()}");
                throw;
            }

            // -----------------------------------------------------------------------
            // 2) Determine the sync direction and update the job
            // -----------------------------------------------------------------------
            DateTime? initialWriteDate = null;
            try
            {
                SetSyncSource(_job, out initialWriteDate);

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

            // -----------------------------------------------------------------------
            // 3) Now check the child jobs
            // -----------------------------------------------------------------------
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
                        if (!IsConsistent(_job, initialWriteDate))
                        {
                            // Job is inconsistent, cancel the flow and leave it "in progress",
                            // so it will be restarted later.
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
                            Job_Source_Record_ID = request.JobSourceRecordID
                        };

                        var entry = _db.GetJobBy(_job.Job_ID, childJob.Job_Source_System, childJob.Job_Source_Model, childJob.Job_Source_Record_ID);

                        // If the child job wasn't created yet, create it
                        if (entry == null)
                        {
                            Log.LogInformation($"Creating child job for job ({_job.Job_ID}) for [{childJob.Job_Source_System}] {childJob.Job_Source_Model} ({childJob.Job_Source_Record_ID}).");
                            _db.CreateJob(childJob);

                            entry = childJob;
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
                            flow.Start(flowManager, entry, DateTime.UtcNow, ref requireRestart);

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
                        requireRestart = true;
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

            try
            {
                // -----------------------------------------------------------------------
                // 4) Transformation
                // -----------------------------------------------------------------------
                if (!IsConsistent(_job, initialWriteDate))
                {
                    // Job is inconsistent, cancel the current flow. Make sure
                    // To do this outside the try-finally block, because we want
                    // the job to stay "in progress".
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
        }

        /// <summary>
        /// Inform the sync flow that a specific model is needed as a child job
        /// for the current flow.
        /// </summary>
        /// <param name="system">The job source system.</param>
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

                if (onlineInfo == null)
                    throw new ModelNotFoundException(
                        SosyncSystem.FSOnline,
                        job.Job_Source_Model,
                        job.Job_Source_Record_ID);

                if (onlineInfo.ForeignID != null)
                    studioInfo = GetStudioInfo(onlineInfo.ForeignID.Value);
            }
            else
            {
                studioInfo = GetStudioInfo(job.Job_Source_Record_ID);

                if (studioInfo == null)
                    throw new ModelNotFoundException(
                        SosyncSystem.FundraisingStudio,
                        job.Job_Source_Model,
                        job.Job_Source_Record_ID);

                if (studioInfo.ForeignID != null)
                    onlineInfo = GetOnlineInfo(studioInfo.ForeignID.Value);
            }

            writeDate = null;

            // Get the attributes for the model names
            var studioAtt = this.GetType().GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
            var onlineAtt = this.GetType().GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();

            if (onlineInfo != null && onlineInfo.ForeignID.HasValue && !onlineInfo.SosyncWriteDate.HasValue)
                throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fso]: sosync_fs_id={onlineInfo.ForeignID} but sosync_write_date=null");

            if (studioInfo != null && studioInfo.ForeignID.HasValue && !studioInfo.SosyncWriteDate.HasValue)
                throw new SyncerException($"Invalid state in model {job.Job_Source_Model} [fs]: sosync_fso_id={onlineInfo.ForeignID} but sosync_write_date=null.");

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
                    writeDate = studioInfo.SosyncWriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FundraisingStudio,
                        studioAtt.Name,
                        studioInfo.ID,
                        SosyncSystem.FSOnline,
                        onlineAtt.Name,
                        studioInfo.ForeignID,
                        "");
                }
                else
                {
                    // The online model was newer
                    writeDate = onlineInfo.SosyncWriteDate;
                    UpdateJobSourceAndTarget(
                        job,
                        SosyncSystem.FSOnline,
                        onlineAtt.Name,
                        onlineInfo.ID,
                        SosyncSystem.FundraisingStudio,
                        studioAtt.Name,
                        onlineInfo.ForeignID,
                        "");
                }
            }
            else if (onlineInfo != null && studioInfo == null)
            {
                // The online model is not yet in studio
                writeDate = onlineInfo.SosyncWriteDate;
                UpdateJobSourceAndTarget(
                    job,
                    SosyncSystem.FSOnline,
                    onlineAtt.Name,
                    onlineInfo.ID,
                    SosyncSystem.FundraisingStudio,
                    studioAtt.Name,
                    null,
                    "");
            }
            else if (onlineInfo == null && studioInfo != null)
            {
                // The studio model is not yet in online
                writeDate = studioInfo.SosyncWriteDate;
                UpdateJobSourceAndTarget(
                    job,
                    SosyncSystem.FundraisingStudio,
                    studioAtt.Name,
                    studioInfo.ID,
                    SosyncSystem.FSOnline,
                    onlineAtt.Name,
                    null,
                    "");
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
        /// <param name="writeDate">The write date of the job since the last read.</param>
        /// <returns></returns>
        private bool IsConsistent(SyncJob job, DateTime? writeDate)
        {
            Log.LogDebug($"Checking model consistency");

            ModelInfo currentInfo = null;

            if (job.Sync_Source_System == SosyncSystem.FSOnline)
                currentInfo = GetOnlineInfo(job.Sync_Source_Record_ID.Value);
            else
                currentInfo = GetStudioInfo(job.Sync_Source_Record_ID.Value);

            if (!currentInfo.SosyncWriteDate.HasValue)
                throw new MissingSosyncWriteDateException(
                    job.Sync_Source_System,
                    job.Sync_Source_Model,
                    job.Sync_Source_Record_ID.Value);

            // Do not use any tolerance here. It's a check to see if the provided write date
            // is different from what is currently in the model
            if (writeDate.HasValue && writeDate.Value == currentInfo.SosyncWriteDate.Value)
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

                db.UpdateJob(_job);
            }
        }

        protected void UpdateSyncSourceData(string data)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Source_Data");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Source_Data = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                db.UpdateJob(_job);
            }
        }

        protected void UpdateSyncTargetRequest(string requestData)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Request");

            using (var db = Service.GetService<DataService>())
            {
                _job.Sync_Target_Request = requestData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                db.UpdateJob(_job);
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
                db.UpdateJob(_job);
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

                job.Job_Log = log;
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                db.UpdateJob(job);
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

                db.UpdateJob(_job);
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

                db.UpdateJob(_job);
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

                db.UpdateJob(_job);
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

                db.UpdateJob(_job);
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

                db.UpdateJob(job);
            }
        }

        private void UpdateJobRunCount(SyncJob job)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: run_count");

            using (var db = Service.GetService<DataService>())
            {
                job.Job_Run_Count += 1;
                job.Job_Last_Change = DateTime.UtcNow;

                db.UpdateJob(job);
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

                db.UpdateJob(_job);
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

                db.UpdateJob(_job);
            }
        }

        public void Dispose()
        {
            _db.Dispose();
        }
        #endregion
    }
}
