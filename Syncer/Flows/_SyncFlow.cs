using dadi_data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

            _requiredChildJobs = new List<ChildJobRequest>();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Configure the flow for the sync direction online to studio.
        /// </summary>
        /// <param name="job">The sync job.</param>
        protected abstract void ConfigureOnlineToStudio(SyncJob job);

        /// <summary>
        /// Configure the flow for the sync direction studio to online.
        /// </summary>
        /// <param name="job">The sync job.</param>
        protected abstract void ConfigureStudioToOnline(SyncJob job);

        /// <summary>
        /// Starts the data flow.
        /// </summary>
        /// <param name="job"></param>
        public void Start(SyncJob job)
        {
            UpdateJobStart(job);
            try
            {

                // 1) Throws and exception, if the run count reaches/exceeds maximum
                CheckRunCount(job, 5);

                // 2) Check if child jobs are there, or create them


                // 3) Process child jobs
                foreach (var childJob in job.Children)
                {
                    //Process(childJob);
                }

                // 4) Read write dates of both models



                //if (_rnd.Next(1, 10) <= 3)
                //    throw new Exception("Simulated exception with 30% chance.");

                UpdateJobSuccess(job);
            }
            catch (Exception ex)
            {
                UpdateJobError(job, SosyncError.SourceData, ex.ToString());
            }
        }

        protected SyncFlow RequireModel(string system, string model, int id)
        {
            _requiredChildJobs.Add(new ChildJobRequest(system, model, id));
            return this;
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
        private void UpdateJobSuccess(SyncJob job)
        {
            _log.LogInformation($"Updating job {job.Job_ID}: job success");

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
