using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for any sync flow.
    /// </summary>
    public abstract class SyncFlow
    {
        #region Members
        private IServiceProvider _svc;
        private OdooService _odoo;
        private MdbService _mdb;
        private ILogger<SyncFlow> _log;
        private CancellationToken _cancelToken;

        private List<ChildJobRequest> _requiredChildJobs;
        private SyncJob _job;
        #endregion

        #region Properties
        protected ILogger<SyncFlow> Log
        {
            get { return _log; }
        }

        protected IServiceProvider Service
        {
            get { return _svc; }
        }

        protected OdooService Odoo
        {
            get { return _odoo; }
        }

        protected MdbService Mdb
        {
            get { return _mdb; }
        }

        public CancellationToken CancelToken
        {
            get { return _cancelToken; }
            set { _cancelToken = value; }
        }
        #endregion

        #region Constructors
        public SyncFlow(IServiceProvider svc)
        {
            _svc = svc;
            _log = _svc.GetService<ILogger<SyncFlow>>();
            _odoo = _svc.GetService<OdooService>();
            _mdb = _svc.GetService<MdbService>();

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
        /// Starts the data flow.
        /// </summary>
        /// <param name="_job"></param>
        public void Start(SyncJob job)
        {
            _job = job;

            UpdateJobStart();

            // -----------------------------------------------------------------------
            // 1) First off, check run count (and eventually throw exception)
            // -----------------------------------------------------------------------
            try
            {
                CheckRunCount(20);
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
                UpdateJobError(SosyncError.SourceData, $"2) Sync direction:\n{ex.ToString()}");
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

                    foreach (ChildJobRequest jobRequest in _requiredChildJobs)
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
#warning TODO: Somehow differentiate between child creation and processing errors
                UpdateJobError(SosyncError.SourceData, $"3) Child jobs:\n{ex.ToString()}");
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
                UpdateJobError(SosyncError.SourceData, $"4) Transformation/sync:\n{ex.ToString()}");
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

        protected void UpdateSyncTargetDataBeforeUpdate(string data)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: Sync_Target_Data_Before_Update");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_Target_Data_Before_Update = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        protected void UpdateSyncSourceData(string data)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: Sync_Source_Data");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_Source_Data = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        protected void UpdateSyncTargetRequest(string requestData)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: Sync_Target_Request");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_Target_Request = requestData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        protected void UpdateSyncTargetAnswer(string answerData)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: Sync_Target_Answer");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_Target_Answer = answerData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(_job);
            }
            UpdateJobFso();
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

                if (onlineInfo == null)
                    throw new ModelNotFoundException(
                        SosyncSystem.FundraisingStudio,
                        job.Job_Source_Model,
                        job.Job_Source_Record_ID);

                if (studioInfo.ForeignID != null)
                    onlineInfo = GetOnlineInfo(studioInfo.ForeignID.Value);
            }

            // If the studio model had a write date, convert it to UTC. Because
            // FundraisingStudio always saves local time stamps.
            if (studioInfo.WriteDate.HasValue)
                studioInfo.WriteDate = studioInfo.WriteDate.Value.ToUniversalTime();

            writeDate = null;

            // Get the attributes for the model names
            var studioAtt = this.GetType().GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
            var onlineAtt = this.GetType().GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();

            // Now update the job information depending on the available
            // model infos
            if (onlineInfo != null && studioInfo != null)
            {
                // Both systems already have the model, check write date
                // and decide source
                var diff = GetWriteDateDifference(onlineInfo.WriteDate.Value, studioInfo.WriteDate.Value);
                if (diff.TotalMilliseconds == 0)
                {
                    // Both models are within tolerance, and considered up to date.
                    writeDate = null;
                    UpdateJobSourceAndTarget(job, "", "", null, "", "", null, "Model up to date.");
                }
                else if(diff.TotalMilliseconds < 0)
                {
                    // The studio model was newer
                    writeDate = studioInfo.WriteDate;
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
                    writeDate = onlineInfo.WriteDate;
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
                writeDate = onlineInfo.WriteDate;
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
                writeDate = studioInfo.WriteDate;
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
            ModelInfo currentInfo = null;

            if (job.Sync_Source_System == SosyncSystem.FSOnline)
                currentInfo = GetOnlineInfo(job.Sync_Source_Record_ID.Value);
            else
            {
                currentInfo = GetStudioInfo(job.Sync_Source_Record_ID.Value);

                // Studio only uses local times, so convert studio time to UTC before working with it
                if (currentInfo.WriteDate.HasValue)
                    currentInfo.WriteDate = currentInfo.WriteDate.Value.ToUniversalTime();
            }

            // Do not use any tolerance here. It's a check to see if the provided write date
            // is different from what is currently in the model
            if (writeDate.Value == currentInfo.WriteDate.Value)
                return true;

            return false;
        }

        /// <summary>
        /// Returns the difference of two date time fields. If
        /// the difference is within a certain tolerance, the
        /// difference is returned as zero.
        /// </summary>
        /// <param name="onlineWriteDateUTC">The FSO write date UTC time stamp.</param>
        /// <param name="studioWriteDateUTC">The Studio UTC time stamp.</param>
        /// <returns></returns>
        private TimeSpan GetWriteDateDifference(DateTime onlineWriteDateUTC, DateTime studioWriteDateUTC)
        {
            var toleranceMS = 1000;
            // FSO has UTC times, FundraisingStudio has local times
            var result = onlineWriteDateUTC - studioWriteDateUTC;

            _log.LogInformation($"GetWriteDateDifference() - Write date diff: {Math.Abs(result.TotalMilliseconds)} Tolerance: {toleranceMS}");

            // If the difference is within the tolerance,
            // return zero
            if (Math.Abs(result.TotalMilliseconds) <= toleranceMS)
                result = TimeSpan.FromMilliseconds(0);

            return result;
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
            _log.LogInformation($"Updating job {job.Job_ID}: check source");

            using (var db = _svc.GetService<DataService>())
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
            UpdateJobFso();
        }

        /// <summary>
        /// Updates the job, indicating that child processing started.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        private void UpdateJobChildStart(SyncJob job)
        {
            _log.LogInformation($"Updating job { job.Job_ID}: child start");

            using (var db = _svc.GetService<DataService>())
            {
                job.Child_Job_Start = DateTime.UtcNow;
                job.Job_Last_Change = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing finished.
        /// </summary>
        private void UpdateJobChildEnd()
        {
            _log.LogInformation($"Updating job { _job.Job_ID}: child end");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Child_Job_End = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates the job, indicating that the tansformation started.
        /// </summary>
        private void UpdateJobSyncStart()
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: transformation/sync start");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_Start = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates the job, indicating that the transformation finished.
        /// </summary>
        private void UpdateJobSyncEnd()
        {
            _log.LogInformation($"Updating job { _job.Job_ID}: transformation/sync end");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Sync_End = DateTime.UtcNow;
                _job.Job_Last_Change = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates the job, indicating processing started.
        /// </summary>
        private void UpdateJobStart()
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: job start");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Job_State = SosyncState.InProgress;
                _job.Job_Start = DateTime.Now.ToUniversalTime();
                _job.Job_Run_Count += 1;
                _job.Job_Last_Change = DateTime.UtcNow;
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        /// <summary>
        /// Updates the job, indicating the processing was
        /// done successfully.
        /// </summary>
        /// <param name="wasInSync">Set to true, if the job was
        /// finished because it was already in sync.</param>
        private void UpdateJobSuccess(bool wasInSync)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: job done {(wasInSync ? " (model already in sync)" : "")}");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Job_State = SosyncState.Done;
                _job.Job_End = DateTime.Now.ToUniversalTime();
                _job.Job_Last_Change = DateTime.UtcNow;
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        /// <summary>
        /// Updates the job, indicating an an error occured while processing.
        /// </summary>
        /// <param name="errorCode">Use one of the values from <see cref="SosyncError"/>.</param>
        /// <param name="errorText">The custom error text.</param>
        private void UpdateJobError(string errorCode, string errorText)
        {
            _log.LogInformation($"Updating job {_job.Job_ID}: job error");

            using (var db = _svc.GetService<DataService>())
            {
                _job.Job_State = SosyncState.Error;
                _job.Job_End = DateTime.UtcNow;
                _job.Job_Error_Code = errorCode;
                _job.Job_Error_Text = errorText;
                _job.Job_Last_Change = DateTime.UtcNow;
                db.UpdateJob(_job);
            }
            UpdateJobFso();
        }

        /// <summary>
        /// Synchronizes the specified job to FSO and updates the job_fso_id accordingly.
        /// </summary>
        private void UpdateJobFso()
        {
            _log.LogInformation($"Updating job {_job.Job_ID} in FSOnline");

            if (_job.Job_Fso_ID.HasValue)
            {
                // If job_fso_id is already known, just update fso
                _odoo.Client.UpdateModel<SyncJob>("sosync.job", _job, _job.Job_Fso_ID.Value);
            }
            else
            {
                // Without the id, first check if fso already has the job_id.
                // Create it if it doesn't, else get it and update the id,
                // then update the job.
                // If more than one result is returend, SingleOrDefault throws
                // an exception
                int foundJobId = _odoo.Client
                    .SearchModelByField<SyncJob, int>("sosync.job", x => x.Job_ID, _job.Job_ID)
                    .SingleOrDefault();

                if (foundJobId == 0)
                {
                    // Job didn't exist yet, create it
                    int newId = _odoo.Client.CreateModel<SyncJob>("sosync.job", _job);
                    _job.Job_Fso_ID = newId;

                    using (var db = _svc.GetService<DataService>())
                        db.UpdateJob(_job, x => x.Job_Fso_ID);
                }
                else
                {
                    // Job was found, update its id in sync_table, then update it
                    _job.Job_Fso_ID = foundJobId;

                    using (var db = _svc.GetService<DataService>())
                        db.UpdateJob(_job, x => x.Job_Fso_ID);

                    _odoo.Client.UpdateModel<SyncJob>("sosync.job", _job, _job.Job_Fso_ID.Value);
                }
            }
        }
        #endregion
    }
}
