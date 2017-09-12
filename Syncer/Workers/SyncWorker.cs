using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Linq;
using System.Threading;
using WebSosync.Common;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Models;

namespace Syncer.Workers
{
    /// <summary>
    /// The sync worker represents the background thread, loading and processing jobs.
    /// </summary>
    public class SyncWorker : WorkerBase
    {
        #region Members
        private IServiceProvider _svc;
        private FlowService _flowManager;
        private ILogger<SyncWorker> _log;
        private OdooService _odoo;
        private TimeService _timeSvc;
        private IBackgroundJob<ProtocolWorker> _protocolJob;
   #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="SyncWorker"/> class.
        /// </summary>
        /// <param name="options">Options to be used for data connections etc.</param>
        public SyncWorker(
            IServiceProvider svc, 
            SosyncOptions options, 
            FlowService flowManager, 
            ILogger<SyncWorker> logger, 
            OdooService odoo,
            TimeService timeSvc,
            IBackgroundJob<ProtocolWorker> protocolJob
            )
            : base(options)
        {
            _svc = svc;
            _flowManager = flowManager;
            _log = logger;
            _odoo = odoo;
            _timeSvc = timeSvc;
            _protocolJob = protocolJob;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
            var reCheckTimeMin = 30;

            // Get only the first open job and its hierarchy,
            // and build the tree in memory
            var loadTimeUTC = DateTime.UtcNow;
            var job = GetNextOpenJob();

            while (job != null)
            {
                try
                {
                    // Check server drift once per batch (a batch is every job the while loop captures),
                    // but also check every reCheckMin minutes within a batch
                    if (_timeSvc.LastDriftCheck == null || (DateTime.UtcNow - _timeSvc.LastDriftCheck.Value).Minutes >= reCheckTimeMin)
                        _timeSvc.ThrowOnTimeDrift();

                    if (_timeSvc.DriftLockUntil.HasValue && _timeSvc.DriftLockUntil > DateTime.UtcNow)
                    {
                        var expiresInMs = (int)(_timeSvc.DriftLockUntil.Value - DateTime.UtcNow).TotalMilliseconds;

                        if (expiresInMs < 0)
                            expiresInMs = 0;

                        _log.LogError($"Synchronization locked due to time drift. Lock expires in {SpecialFormat.FromMilliseconds(expiresInMs)} ({_timeSvc.DriftLockUntil.Value.ToString("o")})");
                        return;
                    }

                    UpdateJobStart(job, loadTimeUTC);

                    // Get the flow for the job source model, and start it
                    using (SyncFlow flow = (SyncFlow)_svc.GetService(_flowManager.GetFlow(job.Job_Source_Model)))
                    {
                        bool requireRestart = false;
                        flow.Start(_flowManager, job, loadTimeUTC, ref requireRestart);

                        if (new string[] { "done", "error" }.Contains((job.Job_State ?? "").ToLower()))
                            CloseAllPreviousJobs(job);

                        if (requireRestart)
                            RaiseRequireRestart($"{flow.GetType().Name} has unfinished child jobs");

                        // Throttling
                        if (Configuration.Throttle_ms > 0)
                        {
                            var consumedTimeMs = (int)(DateTime.UtcNow - loadTimeUTC).TotalMilliseconds;
                            var remainingTimeMs = Configuration.Throttle_ms - consumedTimeMs;

                            if (remainingTimeMs > 0)
                            {
                                _log.LogInformation($"Throttle set to {Configuration.Throttle_ms}ms, job took {consumedTimeMs}ms, sleeping {remainingTimeMs}ms");
                                Thread.Sleep(remainingTimeMs);
                            }
                            else
                            {
                                _log.LogDebug($"Job time ({consumedTimeMs}ms) exceeded throttle time ({Configuration.Throttle_ms}ms), continuing at full speed.");
                            }
                        }

                        // Stop processing the queue if cancellation was requested
                        if (CancellationToken.IsCancellationRequested)
                        {
                            // Raise the cancelling event
                            RaiseCancelling();
                            // Clean up here, if necessary
                        }
                    }
                }
                catch(TimeDriftException ex)
                {
                    // Set the drift lock to re-check time + 5 seconds
                    // to make sure another drift check is done before
                    // the lock expires
                    _timeSvc.DriftLockUntil = DateTime.UtcNow.AddMinutes(reCheckTimeMin).AddSeconds(5);

                    var waitTimeMs = 1000 * 60 * reCheckTimeMin + 5000;
                    _log.LogError($"{ex.Message} Locking sync for {SpecialFormat.FromMilliseconds(waitTimeMs)}.");
                }
                catch(Exception ex)
                {
                    _log.LogError(ex.ToString());
                    UpdateJobError(job, ex.ToString());
                }

                // All finished jobs get flagged for beeing synced to fso
                if (job.Job_State == SosyncState.Done || job.Job_State == SosyncState.Error)
                    UpdateJobAllowSync(job);

                // Start the background job for synchronization of sync jobs to fso
                _protocolJob.Start();

                // Get the next open job
                loadTimeUTC = DateTime.UtcNow;
                job = GetNextOpenJob();
            }
        }

        private SyncJob GetNextOpenJob()
        {
            using (var db = _svc.GetService<DataService>())
            {
                var result = db.GetFirstOpenJobHierarchy().ToTree(
                        x => x.Job_ID,
                        x => x.Parent_Job_ID,
                        x => x.Children)
                        .SingleOrDefault();

                return result;
            }
        }

        private void CloseAllPreviousJobs(SyncJob job)
        {
            if (!job.Job_Source_Sosync_Write_Date.HasValue)
                throw new SyncerException($"Submitted {nameof(job.Job_Source_Sosync_Write_Date)} was null, cannot close previous jobs (job_id = {job.Job_ID})");

            using (var db = _svc.GetService<DataService>())
            {
                var affected = db.ClosePreviousJobs(job);

                if (affected > 0)
                    _log.LogInformation($"Closed {affected} jobs with job_source_sosync_write_date less than {job.Job_Source_Sosync_Write_Date.Value.ToString("o")}");
            }
        }

        /// <summary>
        /// Updates the job, indicating processing started.
        /// </summary>
        private void UpdateJobStart(SyncJob job, DateTime loadTimeUTC)
        {
            _log.LogDebug($"Updating job {job.Job_ID}: job start");

            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.InProgress;
                job.Job_Start = loadTimeUTC;
                job.Job_Last_Change = DateTime.UtcNow;

                db.UpdateJob(job);
            }
        }

        private void UpdateJobError(SyncJob job, string message)
        {
            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.Error;
                job.Job_Log = message;
                job.Job_Last_Change = DateTime.UtcNow;
                job.Job_End = DateTime.UtcNow;

                db.UpdateJob(job);
            }
        }

        private void UpdateJobAllowSync(SyncJob job)
        {
            // First recursively update all child jobs to be synced
            if (job.Children != null && job.Children.Count > 0)
            {
                foreach (var childJob in job.Children)
                    UpdateJobAllowSync(childJob);
            }

            // Then update the job
            using (var db = _svc.GetService<DataService>())
            {
                // Don't update job_last_change on job-sync related fields
                job.Job_To_FSO_Can_Sync = true;
                db.UpdateJob(job);
            }
        }
        #endregion
    }
}