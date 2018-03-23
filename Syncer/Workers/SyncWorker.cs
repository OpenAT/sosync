using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private SosyncOptions _conf;
        private FlowService _flowService;
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
            FlowService flowService, 
            ILogger<SyncWorker> logger, 
            OdooService odoo,
            TimeService timeSvc,
            IBackgroundJob<ProtocolWorker> protocolJob
            )
            : base(options)
        {
            _svc = svc;
            _conf = options;
            _flowService = flowService;
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
            using (var db = GetDb())
            {
                var reCheckTimeMin = 30;

                // Get only the first open job and its hierarchy,
                // and build the tree in memory
                var loadTimeUTC = DateTime.UtcNow;

                var s = new Stopwatch();
                s.Start();
                var job = GetNextOpenJob(db);
                s.Stop();

                while (job != null && !CancellationToken.IsCancellationRequested)
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

                        job.Job_Log += $"GetNextOpenJob: {s.Elapsed.TotalMilliseconds.ToString("0")} ms\n";
                        UpdateJobStart(job, loadTimeUTC);

                        // Get the flow for the job source model, and start it
                        var constructorParams = new object[] { _svc, _conf };
                        using (SyncFlow flow = (SyncFlow)Activator.CreateInstance(_flowService.GetFlow(job.Job_Source_Type, job.Job_Source_Model), constructorParams))
                        {
                            bool requireRestart = false;
                            string restartReason = "";
                            flow.Start(_flowService, job, loadTimeUTC, ref requireRestart, ref restartReason);

                            if (new string[] { "done", "error" }.Contains((job.Job_State ?? "").ToLower()))
                                CloseAllPreviousJobs(job);

                            if (requireRestart)
                                RaiseRequireRestart($"{flow.GetType().Name}: {restartReason}");

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
                    catch (TimeDriftException ex)
                    {
                        // Set the drift lock to re-check time + 5 seconds
                        // to make sure another drift check is done before
                        // the lock expires
                        _timeSvc.DriftLockUntil = DateTime.UtcNow.AddMinutes(reCheckTimeMin).AddSeconds(5);

                        var waitTimeMs = 1000 * 60 * reCheckTimeMin + 5000;
                        _log.LogError($"{ex.Message} Locking sync for {SpecialFormat.FromMilliseconds(waitTimeMs)}.");
                    }
                    catch (Exception ex)
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
                    s.Reset();
                    s.Start();
                    job = GetNextOpenJob(db);
                    s.Stop();
                }
            }
        }

        private DataService GetDb()
        {
            return new DataService(_conf);
        }

        private SyncJob GetNextOpenJob(DataService db)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            var result = new List<SyncJob>(db.GetFirstOpenJobHierarchy()).ToTree(
                    x => x.Job_ID,
                    x => x.Parent_Job_ID,
                    x => x.Children)
                    .SingleOrDefault();

            s.Stop();
            if (result != null)
                _log.LogDebug($"Job {result.Job_ID}: {nameof(GetNextOpenJob)} elapsed: {s.ElapsedMilliseconds} ms");

            return result;
        }

        private void CloseAllPreviousJobs(SyncJob job)
        {
            if (!job.Job_Source_Sosync_Write_Date.HasValue)
                throw new SyncerException($"Submitted {nameof(job.Job_Source_Sosync_Write_Date)} was null, cannot close previous jobs (job_id = {job.Job_ID})");

            using (var db = GetDb())
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

            using (var db = GetDb())
            {
                job.Job_State = SosyncState.InProgress;
                job.Job_Start = loadTimeUTC;
                job.Job_Last_Change = DateTime.UtcNow;

                db.UpdateJob(job);
            }
        }

        private void UpdateJobError(SyncJob job, string message)
        {
            using (var db = GetDb())
            {
                job.Job_State = SosyncState.Error;
                // Set the job_log if it's empty, otherwise concatenate it
                job.Job_Error_Text = string.IsNullOrEmpty(job.Job_Error_Text) ? message : job.Job_Error_Text + "\n\n" + message;
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
            using (var db = GetDb())
            {
                // Don't update job_last_change on job-sync related fields
                job.Job_To_FSO_Can_Sync = true;
                db.UpdateJob(job);
            }
        }
        #endregion
    }
}