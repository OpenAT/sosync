using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
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
        /// Get the write date for the model in online.
        /// </summary>
        /// <param name="onlineID">The ID for the model.</param>
        /// <returns></returns>
        protected abstract ModelInfo GetOnlineInfo(int onlineID);

        /// <summary>
        /// Get the write date for the model in studio.
        /// </summary>
        /// <param name="studioID">The ID for the model.</param>
        /// <returns></returns>
        protected abstract ModelInfo GetStudioInfo(int studioID);

        /// <summary>
        /// Configure the flow for the sync direction online to studio.
        /// This configuration can at some point be read from meta data
        /// from fs online.
        /// </summary>
        /// <param name="job">The sync job.</param>
        protected abstract void SetupOnlineToStudioChildJobs(int onlineID);

        /// <summary>
        /// Configure the flow for the sync direction studio to online.
        /// </summary>
        /// <param name="job">The sync job.</param>
        protected abstract void SetupStudioToOnlineChildJobs(int studioID);

        /// <summary>
        /// Read the studio model with the given ID and transform it
        /// to an online model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="studioID">The ID of the studio model to be loaded.</param>
        protected abstract void TransformToOnline(int studioID);

        /// <summary>
        /// Read the online model with the given ID and transform it
        /// to a studio model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="onlineID">The ID of the online model to be loaded.</param>
        protected abstract void TransformToStudio(int onlineID);

        /// <summary>
        /// Starts the data flow.
        /// </summary>
        /// <param name="job"></param>
        public void Start(SyncJob job)
        {
            UpdateJobStart(job);
            try
            {
                // -----------------------------------------------------------------------
                // 1) First off, check run count (and eventually throw exception)
                // -----------------------------------------------------------------------
                CheckRunCount(job, 20);

                // -----------------------------------------------------------------------
                // 2) Determine the sync direction and update the job
                // -----------------------------------------------------------------------
                DateTime? initialWriteDate = null;
                SetSyncSource(job, out initialWriteDate);

                if (string.IsNullOrEmpty(job.Sync_Source_System))
                {
                    // If the source system could not be determined,
                    // the model was up to date in both systems. Close
                    // the job, and stop this flow
                    UpdateJobSuccess(job, true);
                    return;
                }

                // -----------------------------------------------------------------------
                // 3) Now check the child jobs
                // -----------------------------------------------------------------------

                // The derived sync flow is responsible to provide the configuration
                // for the required child jobs. Though, the sync flow base decides which
                // direction to get the configuration for. 

                if (job.Sync_Source_System == SosyncSystem.FSOnline)
                    SetupOnlineToStudioChildJobs(job.Sync_Source_Record_ID.Value);
                else
                    SetupStudioToOnlineChildJobs(job.Sync_Source_Record_ID.Value);

                // After configuration, we know which child jobs are required.
                // Now check and set them up accordingly.
                // 1) Ignore finished child jobs
                // 2) If child job is open --> Skipped?
                // 3) Create all requested child jobs
                foreach(ChildJobRequest jobRequest in _requiredChildJobs)
                {
                    if (!IsConsistent(job, initialWriteDate))
                    {
                        // Job is inconsistent, leave it "in progress" but cancel the flow
                        return;
                    }

#warning TODO: bubble up the hierarchy to check for circular references
                    // 1) Check if any parent job already has
                    //    syn_source_system, sync_source_model, sync_source_record-id

                    // 2 Create & Process child job


                }

                if (!IsConsistent(job, initialWriteDate))
                {
                    // Job is inconsistent, leave it "in progress" but cancel the flow
                    return;
                }

                // -----------------------------------------------------------------------
                // 4) Transformation
                // -----------------------------------------------------------------------

                if (job.Sync_Source_System == SosyncSystem.FSOnline)
                    TransformToStudio(job.Sync_Source_Record_ID.Value);
                else
                    TransformToOnline(job.Sync_Source_Record_ID.Value);

                // Done - update job success
                UpdateJobSuccess(job, false);
            }
            catch (Exception ex)
            {
                UpdateJobError(job, SosyncError.SourceData, ex.ToString());
            }
        }

        protected void RequestChildJob(string system, string model, int id)
        {
            _requiredChildJobs.Add(new ChildJobRequest(system, model, id));
        }

        /// <summary>
        /// Checks the current run_count value of the job against the
        /// specified maximum value. Throws an <see cref="RunCountException"/>
        /// if the maximum value is reached or exceeded.
        /// </summary>
        /// <param name="job">The job to be checked.</param>
        /// <param name="maxRuns">The number of runs upon an exception is raised.</param>
        private void CheckRunCount(SyncJob job, int maxRuns)
        {
            if (job.Job_Run_Count >= maxRuns)
                throw new RunCountException(job.Job_Run_Count, maxRuns);
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

            if (job.Job_Source_System == SosyncSystem.FSOnline)
            {
                onlineInfo = GetOnlineInfo(job.Job_Source_Record_ID);

                if (onlineInfo.ForeignID != null)
                    studioInfo = GetStudioInfo(onlineInfo.ForeignID.Value);
            }
            else
            {
                studioInfo = GetStudioInfo(job.Job_Source_Record_ID);

                if (studioInfo.ForeignID != null)
                    onlineInfo = GetStudioInfo(studioInfo.ForeignID.Value);
            }

            writeDate = null;

            if (onlineInfo != null && studioInfo != null)
            {
                // Both systems already have the model, check write date
                var diff = GetWriteDateDifference(onlineInfo.WriteDate.Value, studioInfo.WriteDate.Value);
                if (diff.TotalMilliseconds == 0)
                {
                    // Both models are within tolerance, and considered up to date.
                    writeDate = null;
                    UpdateJobSource(job, "", "", null, "Model up to date.");
                }
                else if(diff.TotalMilliseconds < 0)
                {
                    // The studio model was newer
                    writeDate = studioInfo.WriteDate;
                    var att = this.GetType().GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
                    UpdateJobSource(job, SosyncSystem.FundraisingStudio, att.Name, studioInfo.ID, "");
                }
                else
                {
                    // The online model was newer
                    writeDate = onlineInfo.WriteDate;
                    var att = this.GetType().GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();
                    UpdateJobSource(job, SosyncSystem.FundraisingStudio, att.Name, onlineInfo.ID, "");
                }
            }
        }

        /// <summary>
        /// Checks if the specified job is still consistent with the specified
        /// write date.
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
                currentInfo = GetStudioInfo(job.Sync_Source_Record_ID.Value);

            if (GetWriteDateDifference(writeDate.Value, currentInfo.WriteDate.Value).TotalMilliseconds == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Returns the difference of two date time fields. If
        /// the difference is within a certain tolerance, the
        /// difference is returned as zero.
        /// </summary>
        /// <param name="d1">The first time stamp.</param>
        /// <param name="d2">The second time stamp.</param>
        /// <returns></returns>
        private TimeSpan GetWriteDateDifference(DateTime d1, DateTime d2)
        {
            var toleranceMS = 1000;
            var result = d1 - d1;

            // If the difference is within the tolerance,
            // return zero
            if (Math.Abs(result.Milliseconds) <= toleranceMS)
                result = TimeSpan.FromMilliseconds(0);

            return result;
        }

        private void UpdateJobSource(SyncJob job, string system, string model, int? id, string log)
        {
            _log.LogInformation($"Updating job {job.Job_ID}: check source");

            using (var db = _svc.GetService<DataService>())
            {
                job.Sync_Source_System = system;
                job.Sync_Source_Model = model;
                job.Sync_Source_Record_ID = id;
                job.Job_Log = log;
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(job);
            }
            UpdateJobFso(job);
        }

        /// <summary>
        /// Updates the job, indicating processing started.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        private void UpdateJobStart(SyncJob job)
        {
            _log.LogInformation($"Updating job {job.Job_ID}: job start");

            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.InProgress;
                job.Job_Start = DateTime.Now.ToUniversalTime();
                job.Job_Run_Count += 1;
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(job);
            }
            UpdateJobFso(job);
        }

        /// <summary>
        /// Updates the job, indicating the processing was
        /// done successfully.
        /// </summary>
        /// <param name="job"></param>
        /// <param name="wasInSync">Set to true, if the job was
        /// finished because it was already in sync.</param>
        private void UpdateJobSuccess(SyncJob job, bool wasInSync)
        {
            _log.LogInformation($"Updating job {job.Job_ID}: job done {(wasInSync ? " (model already in sync)" : "")}");

            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.Done;
                job.Job_End = DateTime.Now.ToUniversalTime();
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(job);
            }
            UpdateJobFso(job);
        }

        /// <summary>
        /// Updates the job, indicating an an error occured while processing.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        /// <param name="errorCode">Use one of the values from <see cref="SosyncError"/>.</param>
        /// <param name="errorText">The custom error text.</param>
        private void UpdateJobError(SyncJob job, string errorCode, string errorText)
        {
            _log.LogInformation($"Updating job {job.Job_ID}: job error");

            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.Error;
                job.Job_End = DateTime.Now.ToUniversalTime();
                job.Job_Error_Code = errorCode;
                job.Job_Error_Text = errorText;
                job.Job_Last_Change = DateTime.Now.ToUniversalTime();
                db.UpdateJob(job);
            }
            UpdateJobFso(job);
        }

        /// <summary>
        /// Synchronizes the specified job to FSO and updates the job_fso_id accordingly.
        /// </summary>
        /// <param name="job">The job to be synchronized.</param>
        private void UpdateJobFso(SyncJob job)
        {
            _log.LogInformation($"Synchronizing job {job.Job_ID} to FSOnline");

            if (job.Job_Fso_ID.HasValue)
            {
                // If job_fso_id is already known, just update fso
                _odoo.Client.UpdateModel<SyncJob>("sosync.job", job, job.Job_Fso_ID.Value);
            }
            else
            {
                // Without the id, first check if fso already has the job_id.
                // Create it if it doesn't, else get it and update the id,
                // then update the job.
                // If more than one result is returend, SingleOrDefault throws
                // an exception
                int foundJobId = _odoo.Client
                    .SearchModelByField<SyncJob, int>("sosync.job", x => x.Job_ID, job.Job_ID)
                    .SingleOrDefault();

                if (foundJobId == 0)
                {
                    // Job didn't exist yet, create it
                    int newId = _odoo.Client.CreateModel<SyncJob>("sosync.job", job);
                    job.Job_Fso_ID = newId;

                    using (var db = _svc.GetService<DataService>())
                        db.UpdateJob(job, x => x.Job_Fso_ID);
                }
                else
                {
                    // Job was found, update its id in sync_table, then update it
                    job.Job_Fso_ID = foundJobId;

                    using (var db = _svc.GetService<DataService>())
                        db.UpdateJob(job, x => x.Job_Fso_ID);

                    _odoo.Client.UpdateModel<SyncJob>("sosync.job", job, job.Job_Fso_ID.Value);
                }
            }
        }
        #endregion
    }
}
