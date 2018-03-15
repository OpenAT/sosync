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
using System.Text;
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
        private List<ChildJobRequest> _requiredPostChildJobs;
        private SyncJob _job;
        private SosyncOptions _conf;
        #endregion

        #region Properties
        protected ILogger<SyncFlow> Log { get; private set; }
        protected IServiceProvider Service { get; private set; }
        protected OdooService OdooService { get; private set; }
        protected MdbService MdbService { get; private set; }
        protected OdooFormatService OdooFormat { get; private set; }
        protected SerializationService Serializer { get; private set; }

        protected IReadOnlyList<ChildJobRequest> RequiredChildJobs { get { return _requiredChildJobs.AsReadOnly(); } }
        protected IReadOnlyList<ChildJobRequest> RequiredPostTransformChildJobs { get { return _requiredPostChildJobs.AsReadOnly(); } }

        public bool SkipAutoSyncSource { get; set; }

        private StringBuilder _timeLog;

        protected SyncJob Job
        {
            get => _job;
            set => _job = value;
        }

        public CancellationToken CancelToken { get; set; }

        public string StudioModelName { get; set; }
        public string OnlineModelName { get; set; }
        #endregion

        #region Constructors
        public SyncFlow(IServiceProvider svc, SosyncOptions conf)
        {
            Service = svc;
            _conf = conf;
            Log = Service.GetService<ILogger<SyncFlow>>();
            OdooService = Service.GetService<OdooService>();
            MdbService = new MdbService(conf);
            OdooFormat = new OdooFormatService();
            Serializer = new SerializationService();
            SkipAutoSyncSource = false;
            _timeLog = new StringBuilder();

            _requiredChildJobs = new List<ChildJobRequest>();
            _requiredPostChildJobs = new List<ChildJobRequest>();

            SetModelNames();
        }
        #endregion

        #region Methods
        public void LogMilliseconds(string operation, double ms)
        {
            _timeLog.AppendLine($"{operation}: {ms.ToString("0")} ms");
        }

        public string GetTimeLog()
        {
            return _timeLog.ToString();
        }

        protected DataService GetDb()
        {
            return new DataService(_conf);
        }

        private void SetModelNames()
        {
            var studioAtt = this.GetType().GetCustomAttribute<StudioModelAttribute>();
            var onlineAtt = this.GetType().GetCustomAttribute<OnlineModelAttribute>();

            if (studioAtt != null)
                StudioModelName = studioAtt.Name;

            if (onlineAtt != null)
                OnlineModelName = onlineAtt.Name;
        }

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
            var odooID = 0;

            var results = OdooService.Client.SearchByField(modelName, "sosync_fs_id", "=", fsId.ToString()).ToList();

            if (results.Count == 1)
                odooID = results[0];
            else if (results.Count > 1)
                throw new SyncerException($"{nameof(GetFsoIdByFsId)}(): Multiple Odoo-IDs found.");

            if (odooID > 0)
                return odooID;

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

        public void Start(FlowService flowService, SyncJob job, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            if (job == null)
                throw new SyncerException($"Parameter '{nameof(job)}' cannot be null.");

            Job = job;

            StartFlow(flowService, loadTimeUTC, ref requireRestart, ref restartReason);

            Job.Job_Log += $"{(string.IsNullOrEmpty(job.Job_Log) ? "" : "\n\n")}{GetTimeLog()}\n";
            UpdateJob(Job, "Saving job log after finished flow");
        }

        protected abstract void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason);

        protected void HandleChildJobs(
            string childDescription,
            IEnumerable<ChildJobRequest> requestedChildJobs,
            FlowService flowService,
            DateTime? initialWriteDate,
            Stopwatch consistencyWatch,
            ref bool requireRestart,
            ref string restartReason)
        {
            LogMs(0, $"\n{nameof(HandleChildJobs)} start", _job.Job_ID, 0);

            var s = new Stopwatch();
            s.Start();

            if (requestedChildJobs.Count() == 0)
            {
                s.Stop();
                LogMs(0, "ChildJobs", _job.Job_ID, s.ElapsedMilliseconds);

                return;
            }

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

                    foreach (ChildJobRequest request in requestedChildJobs)
                    {
                        if (initialWriteDate.HasValue && !IsConsistent(_job, initialWriteDate, consistencyWatch))
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

                        if (request.ForceDirection)
                        {
                            childJob.Sync_Source_System = request.JobSourceSystem;
                            childJob.Sync_Source_Model = request.JobSourceModel;
                            childJob.Sync_Source_Record_ID = request.JobSourceRecordID;

                            childJob.Sync_Target_System =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio
                                ? SosyncSystem.FSOnline
                                : SosyncSystem.FundraisingStudio;

                            childJob.Sync_Target_Model =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio
                                ? OnlineModelName
                                : StudioModelName;

                            childJob.Sync_Target_Record_ID =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio
                                ? GetFsoIdByFsId(OnlineModelName, request.JobSourceRecordID)
                                : GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), request.JobSourceRecordID);

                            childJob.Job_Log += $"Forcing direction: {request.JobSourceModel} ({request.JobSourceRecordID}) --> {childJob.Sync_Target_Model} ({childJob.Sync_Target_Record_ID})\n";
                        }

                        // Try to fetch a child job with the same main properties
                        SyncJob entry = null;
                        using (var db = GetDb())
                        {
                            entry = db.GetJobBy(_job.Job_ID, childJob.Job_Source_System, childJob.Job_Source_Model, childJob.Job_Source_Record_ID);

                            // If the child job wasn't created yet, create it
                            if (entry == null)
                            {
                                Log.LogInformation($"Creating {childDescription} for job ({_job.Job_ID}) for [{childJob.Job_Source_System}] {childJob.Job_Source_Model} ({childJob.Job_Source_Record_ID}).");
                                db.CreateJob(childJob);
                                _job.Children.Add(childJob);

                                entry = childJob;
                            }
                        }

                        if (entry.Job_State == SosyncState.Error)
                            throw new SyncerException($"{childDescription} ({entry.Job_ID}) for [{entry.Job_Source_System}] {entry.Job_Source_Model} ({entry.Job_Source_Record_ID}) failed.");

                        // If the job is new or marked as "in progress", run it
                        if (entry.Job_State == SosyncState.New || entry.Job_State == SosyncState.InProgress)
                        {
                            Log.LogDebug($"Executing {childDescription} ({entry.Job_ID})");

                            UpdateJobStart(entry, DateTime.UtcNow);

                            // Get the flow for the job source model, and start it
                            SyncFlow flow = (SyncFlow)Service.GetService(flowService.GetFlow(entry.Job_Source_Type, entry.Job_Source_Model));

                            if (request.ForceDirection)
                                flow.SkipAutoSyncSource = true;

                            flow.Start(flowService, entry, DateTime.UtcNow, ref requireRestart, ref restartReason);

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
            LogMs(0, $"ChildJobs ({childDescription})", _job.Job_ID, s.ElapsedMilliseconds);
            s.Reset();
        }

        protected void HandleTransformation(string description, DateTime? initialWriteDate, Stopwatch consistencyWatch, ref bool requireRestart, ref string restartReason)
        {
            LogMs(0, $"\n{nameof(HandleTransformation)} start", _job.Job_ID, 0);

            var s = new Stopwatch();
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

                    Log.LogInformation(description);

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
            LogMs(0, $"{nameof(HandleTransformation)} done", _job.Job_ID, s.ElapsedMilliseconds);
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
            _requiredChildJobs.Add(new ChildJobRequest(system, model, id, false));
        }

        /// <summary>
        /// Inform the sync flow that a specific model is needed as a child job
        /// after the transformation is finished.
        /// </summary>
        /// <param name="system">The job source system, use <see cref="SosyncSystem"/> constants.</param>
        /// <param name="model">The requested source model.</param>
        /// <param name="id">The requested source ID for the model.</param>
        protected void RequestPostTransformChildJob(string system, string model, int id, bool forceDirection)
        {
            _requiredPostChildJobs.Add(new ChildJobRequest(system, model, id, forceDirection));
        }

        /// <summary>
        /// Checks the current run_count value of the job against the
        /// specified maximum value. Throws an <see cref="RunCountException"/>
        /// if the maximum value is reached or exceeded.
        /// </summary>
        /// <param name="maxRuns">The number of runs upon an exception is raised.</param>
        protected void CheckRunCount(int maxRuns)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            try
            {
                if (_job.Job_Run_Count >= maxRuns)
                    throw new RunCountException(_job.Job_Run_Count, maxRuns);
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.RunCounter, $"1) Run counter:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "RunCount", _job.Job_ID, s.ElapsedMilliseconds);
        }

        /// <summary>
        /// Checks if the specified write date is still the same in the source system.
        /// </summary>
        /// <param name="job">The job to be checked.</param>
        /// <param name="sosyncWriteDate">The write date of the job since the last read.</param>
        /// <returns></returns>
        protected bool IsConsistent(SyncJob job, DateTime? sosyncWriteDate, Stopwatch consistencyWatch)
        {
            if (sosyncWriteDate == null)
            {
                Log.LogInformation($"Job ({job.Job_ID}) Skipping consistency check, no {nameof(sosyncWriteDate)} present.");
                return true;
            }

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

        protected void UpdateSyncTargetDataBeforeUpdate(string data)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Data_Before_Update");

            using (var db = GetDb())
            {
                _job.Sync_Target_Data_Before = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncTargetDataBeforeUpdate), db, _job);
            }
        }

        protected void UpdateSyncSourceData(string data)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Source_Data");

            using (var db = GetDb())
            {
                _job.Sync_Source_Data = data;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncSourceData), db, _job);
            }
        }

        protected void UpdateSyncTargetRequest(string requestData)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Request");

            using (var db = GetDb())
            {
                _job.Sync_Target_Request = requestData;
                _job.Job_Last_Change = DateTime.Now.ToUniversalTime();

                UpdateJob(nameof(UpdateSyncTargetRequest), db, _job);
            }
        }

        protected void UpdateSyncTargetAnswer(string answerData, int? createdID)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: Sync_Target_Answer");

            using (var db = GetDb())
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
        protected void UpdateJobSourceAndTarget(SyncJob job, string srcSystem, string srcModel, int? srcId, string targetSystem, string targetModel, int? targetId, string log)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: check source");

            using (var db = GetDb())
            {
                job.Sync_Source_System = srcSystem;
                job.Sync_Source_Model = srcModel;
                job.Sync_Source_Record_ID = srcId;

                job.Sync_Target_System = targetSystem;
                job.Sync_Target_Model = targetModel;
                job.Sync_Target_Record_ID = targetId;

                job.Job_Log = job.Job_Log ?? "" + log ?? "";
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

            using (var db = GetDb())
            {
                job.Child_Job_Start = DateTime.UtcNow;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobChildStart), db, _job);
            }
        }

        protected void UpdateJob(SyncJob job, string description)
        {
            Log.LogDebug($"Updating job { _job.Job_ID}: {description}");

            using (var db = GetDb())
            {
                Job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(description, db, _job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing finished.
        /// </summary>
        private void UpdateJobChildEnd()
        {
            Log.LogDebug($"Updating job { _job.Job_ID}: child end");

            using (var db = GetDb())
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

            using (var db = GetDb())
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

            using (var db = GetDb())
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

            using (var db = GetDb())
            {
                job.Job_State = SosyncState.InProgress;
                job.Job_Start = loadTimeUTC;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobStart), db, _job);
            }
        }

        protected void UpdateJobRunCount(SyncJob job)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: run_count");

            using (var db = GetDb())
            {
                job.Job_Run_Count += 1;
                job.Job_Last_Change = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobRunCount), db, _job);
            }
        }

        private void UpdateJobInconsistent(SyncJob job, int nr)
        {
            Log.LogDebug($"Updating job {job.Job_ID}: job_log");

            using (var db = GetDb())
            {
                job.Job_Log += $"Consistency check {nr} failed, exiting job, leaving it in progress.\n";
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
        protected void UpdateJobSuccess(bool wasInSync)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: job done {(wasInSync ? " (model already in sync)" : "")}");

            using (var db = GetDb())
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
        protected void UpdateJobError(string errorCode, string errorText)
        {
            Log.LogDebug($"Updating job {_job.Job_ID}: job error");

            using (var db = GetDb())
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
            LogMs(1, $"Local DB: {method}", job.Job_ID, s.ElapsedMilliseconds);
        }

        protected void LogMs(int lvl, string name, int? jobId, long ms)
        {
            Log.LogDebug($"Job {jobId ?? _job.Job_ID}: {name} elapsed: {ms} ms");
            LogMilliseconds(name, ms);
        }

        public void Dispose()
        {

        }
        #endregion
    }
}
