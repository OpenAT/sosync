using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
            TimeService timeSvc
            )
            : base(options)
        {
            _svc = svc;
            _conf = options;
            _flowService = flowService;
            _log = logger;
            _odoo = odoo;
            _timeSvc = timeSvc;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
            var jobLimit = _conf.Job_Package_Size.Value;

            using (var db = GetDb())
            {
                // Get only the first open job and its hierarchy,
                // and build the tree in memory
                var loadTimeUTC = DateTime.UtcNow;

                var s = new Stopwatch();
                s.Start();
                var jobs = new Queue<SyncJob>(GetNextOpenJob(db, jobLimit));
                s.Stop();

                var initialJobCount = jobs.Count;
                var reCheckTimeMin = 30;

                while (jobs.Count > 0 && !CancellationToken.IsCancellationRequested)
                {
                    var tasks = new List<Task>();
                    var threadCount = _conf.Max_Threads;

                    var threadWatch = new Stopwatch();
                    threadWatch.Start();
                    try
                    {
                        // Check server times
                        if (IsServerTimeMismatch(reCheckTimeMin))
                            return;

                        // Spin up job threads
                        _log.LogInformation($"Threading: Starting {threadCount} threads");
                        for (var i = 1; i <= threadCount; i++)
                        {
                            if (CancellationToken.IsCancellationRequested)
                                break;

                            _log.LogInformation($"Threading: Starting thread {i}");
                            var threadNumber = Guid.NewGuid();
                            tasks.Add(Task.Run(() => JobThread(ref jobs, DateTime.UtcNow, threadNumber)));
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

                    Task.WaitAll(tasks.ToArray());
                    ThreadService.JobLocks.Clear();
                    Dapper.SqlMapper.PurgeQueryCache();
                    threadWatch.Stop();
                    GC.Collect();

                    _log.LogInformation($"Threading: All threads finished {initialJobCount} jobs in {SpecialFormat.FromMilliseconds((int)threadWatch.Elapsed.TotalMilliseconds)}");

                    // Get the next open job
                    loadTimeUTC = DateTime.UtcNow;
                    s.Reset();
                    s.Start();
                    jobs = new Queue<SyncJob>(GetNextOpenJob(db, jobLimit));
                    s.Stop();
                    initialJobCount = jobs.Count;
                }
            }
        }

        private bool IsServerTimeMismatch(int reCheckTimeMin)
        {
            if (_timeSvc.LastDriftCheck == null || (DateTime.UtcNow - _timeSvc.LastDriftCheck.Value).Minutes >= reCheckTimeMin)
                _timeSvc.ThrowOnTimeDrift();

            if (_timeSvc.DriftLockUntil.HasValue && _timeSvc.DriftLockUntil > DateTime.UtcNow)
            {
                var expiresInMs = (int)(_timeSvc.DriftLockUntil.Value - DateTime.UtcNow).TotalMilliseconds;

                if (expiresInMs < 0)
                    expiresInMs = 0;

                _log.LogError($"Synchronization locked due to time drift. Lock expires in {SpecialFormat.FromMilliseconds(expiresInMs)} ({_timeSvc.DriftLockUntil.Value.ToString("o")})");
                return true;
            }

            return false;
        }

        private void JobThread(ref Queue<SyncJob> jobs, DateTime loadTimeUTC, Guid threadNumber)
        {
            _log.LogInformation($"Thread {threadNumber} started");

            try
            {
                SyncJob threadJob;

                do
                {
                    if (CancellationToken.IsCancellationRequested)
                        break;

                    // Fetch next job from queue
                    threadJob = null;
                    lock (jobs)
                    {
                        if (jobs.Count > 0)
                        {
                            _log.LogInformation($"Thread {threadNumber} fetching a job from memory queue");
                            threadJob = jobs.Dequeue();
                        }
                    }

                    // Process job
                    if (threadJob != null)
                    {
                        _log.LogInformation($"Thread {threadNumber} processing the job");
                        threadJob.Job_Log += $"GetNextOpenJob: n/a ms (comes from thread queue)\n";
                        ProcessJob(threadJob, loadTimeUTC);
                        _log.LogInformation($"Thread {threadNumber} finished job, {jobs.Count} jobs remaining");
                    }
                }
                while (threadJob != null);
            }
            catch (Exception ex)
            {
                _log.LogInformation($"Thread {threadNumber} threw an exception {ex.ToString()}");
                throw;
            }

            _log.LogInformation($"Thread {threadNumber} clean exit");
        }

        private void ProcessJob(SyncJob job, DateTime loadTimeUTC)
        {
            using (var dataService = GetDb())
            {
                try
                {
                    UpdateJobRunCount(dataService, job);
                    UpdateJobStart(dataService, job, loadTimeUTC);

                    // Get the flow for the job source model, and start it
                    var constructorParams = new object[] { _log, _odoo, _conf, _flowService };
                    using (SyncFlow flow = (SyncFlow)Activator.CreateInstance(_flowService.GetFlow(job.Job_Source_Type, job.Job_Source_Model), constructorParams))
                    {
                        bool requireRestart = false;
                        string restartReason = "";
                        flow.Start(_flowService, job, loadTimeUTC, ref requireRestart, ref restartReason);

                        if (new string[] { "done", "error" }.Contains((job.Job_State ?? "").ToLower()))
                        {
                            var s = new Stopwatch();
                            s.Start();
                            CloseAllPreviousJobs(job);
                            s.Stop();
                            _log.LogInformation($"{nameof(CloseAllPreviousJobs)}: {s.Elapsed.TotalMilliseconds} ms");
                        }

                        if (requireRestart)
                            RaiseRequireRestart($"{flow.GetType().Name}: {restartReason}");

                        // Throttling
                        ThrottleJobProcess(loadTimeUTC);

                        // Stop processing the queue if cancellation was requested
                        if (CancellationToken.IsCancellationRequested)
                            RaiseCancelling();
                    }
                }
                catch (SqlException ex)
                {
                    _log.LogError(ex.ToString());
                    UpdateJobError(dataService, job, $"{ex.ToString()}\nProcedure: {ex.Procedure}");
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.ToString());
                    UpdateJobError(dataService, job, ex.ToString());
                }
            }
        }

        private void ThrottleJobProcess(DateTime loadTimeUTC)
        {
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
        }

        private DataService GetDb()
        {
            return new DataService(_conf);
        }

        private IList<SyncJob> GetNextOpenJob(DataService db, int limit)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            var result = new List<SyncJob>(db.GetFirstOpenJobHierarchy(limit)).ToTree(
                    x => x.ID,
                    x => x.Parent_Job_ID,
                    x => x.Children)
                    .ToList();

            s.Stop();
            if (result != null)
                _log.LogDebug($"Loaded {result.Count} jobs: {nameof(GetNextOpenJob)} elapsed: {s.ElapsedMilliseconds} ms");

            return result;
        }

        private void CloseAllPreviousJobs(SyncJob job)
        {
            if (!job.Job_Source_Sosync_Write_Date.HasValue)
            {
                _log.LogWarning($"Submitted {nameof(job.Job_Source_Sosync_Write_Date)} was null, cannot close previous jobs (job_id = {job.ID})");
                return;
            }

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
        private void UpdateJobStart(DataService db, SyncJob job, DateTime loadTimeUTC)
        {
            _log.LogDebug($"Updating job {job.ID}: job start");

            job.Job_State = SosyncState.InProgress;
            job.Job_Start = loadTimeUTC;
            job.Write_Date = DateTime.UtcNow;

            db.UpdateJob(job);
        }

        protected void UpdateJobRunCount(DataService db, SyncJob job)
        {
            _log.LogDebug($"Updating job {job.ID}: run_count");

            job.Job_Run_Count += 1;
            job.Write_Date = DateTime.UtcNow;

            db.UpdateJob(job);
        }

        private void UpdateJobError(DataService db, SyncJob job, string message)
        {
            job.Job_State = SosyncState.Error;
            // Set the job_log if it's empty, otherwise concatenate it
            job.Job_Error_Text = string.IsNullOrEmpty(job.Job_Error_Text) ? message : job.Job_Error_Text + "\n\n" + message;
            job.Write_Date = DateTime.UtcNow;
            job.Job_End = DateTime.UtcNow;

            db.UpdateJob(job);
        }
        #endregion
    }
}