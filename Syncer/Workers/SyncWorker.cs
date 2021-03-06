﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Studio.Utility.Extensions;
using Syncer.Exceptions;
using Syncer.Flows;
using Syncer.Helpers;
using Syncer.Services;
using System;
using System.Collections.Concurrent;
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
        private OdooFormatService _odooFormatService;
        private SerializationService _serializationService;
        private IMailService _mail;
        private IThreadSettings _threadSettings;

        private const int _sleepTime = 5500;
        private static int _sleepCycle;

        private static bool _serviceDead = false;
        private static bool _initialRun = true;
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
            OdooFormatService odooFormatService,
            SerializationService serializationService,
            IMailService mailService,
            IThreadSettings threadSettings
            )
            : base(options)
        {
            _svc = svc;
            _conf = options;
            _flowService = flowService;
            _log = logger;
            _odoo = odoo;
            _timeSvc = timeSvc;
            _odooFormatService = odooFormatService;
            _serializationService = serializationService;
            _mail = mailService;
            _threadSettings = threadSettings;
        }
        #endregion

        #region Methods
        private void UpdateThreads(out int packageSize, out int maxThreads)
        {
            packageSize = _conf.Job_Package_Size.Value;
            maxThreads = _conf.Max_Threads ?? 2;

            if (_threadSettings.IsActive)
            {
                packageSize = _threadSettings.TargetPackageSize ?? 20;
                maxThreads = _threadSettings.TargetMaxThreads ?? 2;
            }

            _threadSettings.CurrentPackageSize = packageSize;
            _threadSettings.CurrentMaxThreads = maxThreads;
        }

        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
            if (_serviceDead)
                return;

            var reCheckTimeMin = 30;

            try
            {
                // Check server times
                ThrowOnMismatchedServerTimes(reCheckTimeMin);

                var lastJobCount = 0;

                int jobLimit = 0;
                int threadCount = 0;

                UpdateThreads(out jobLimit, out threadCount);

                using (var db = GetDb())
                {
                    // Get only the first open job and its hierarchy,
                    // and build the tree in memory
                    var loadTimeUTC = DateTime.UtcNow;

                    var s = new Stopwatch();
                    s.Start();
                    var jobs = new ConcurrentQueue<SyncJob>(GetNextOpenJob(db, jobLimit));
                    s.Stop();
                    _log.LogInformation($"Loaded {jobs.Count} in {SpecialFormat.FromMilliseconds((int)s.Elapsed.TotalMilliseconds)}");

                    lastJobCount = jobs.Count;

                    if (_initialRun)
                    {
                        CheckInProgress(jobs);
                        _initialRun = false;
                    }

                    var initialJobCount = jobs.Count;

                    var threadWatch = new Stopwatch();
                    while (jobs.Count > 0 && !CancellationToken.IsCancellationRequested)
                    {
                        var tasks = new List<Task>();

                        UpdateThreads(out jobLimit, out threadCount);

                        threadWatch.Start();

                        // Check server times
                        ThrowOnMismatchedServerTimes(reCheckTimeMin);

                        // Spin up job threads
                        var threadStartWatch = Stopwatch.StartNew();
                        _log.LogInformation($"Threading: Starting {threadCount} threads");
                        for (var i = 1; i <= threadCount; i++)
                        {
                            if (CancellationToken.IsCancellationRequested)
                                break;

                            _log.LogInformation($"Threading: Starting thread {i}");
                            var threadNumber = Guid.NewGuid();
                            tasks.Add(Task.Run(() => JobThread(ref jobs, DateTime.UtcNow, threadNumber)));
                        }
                        threadStartWatch.Stop();
                        _log.LogInformation($"Threading: Needed {SpecialFormat.FromMilliseconds((int)threadStartWatch.Elapsed.TotalMilliseconds)} for {threadCount} threads");

                        Task.WaitAll(tasks.ToArray());
                        threadWatch.Stop();
                        _log.LogInformation($"All threads finished in {SpecialFormat.FromMilliseconds((int)threadWatch.Elapsed.TotalMilliseconds)}.");
                        threadWatch.Reset();

                        ThreadService.JobLocks.Clear();

                        ArchiveJobs();

                        Dapper.SqlMapper.PurgeQueryCache();
                        GC.Collect();

                        _log.LogInformation($"Threading: All threads finished {initialJobCount} jobs in {SpecialFormat.FromMilliseconds((int)threadWatch.Elapsed.TotalMilliseconds)}");

                        // Get the next open job
                        loadTimeUTC = DateTime.UtcNow;
                        s.Reset();
                        s.Start();
                        jobs = new ConcurrentQueue<SyncJob>(GetNextOpenJob(db, jobLimit));
                        s.Stop();
                        _log.LogInformation($"Loaded {jobs.Count} in {SpecialFormat.FromMilliseconds((int)s.Elapsed.TotalMilliseconds)}");

                        initialJobCount = jobs.Count;
                    }

                    // Before ending the worker, always do another archive run,
                    // unless a cancellation is pending
                    if (!CancellationToken.IsCancellationRequested)
                        ArchiveJobs();

                    // If there were jobs, always reset cycle
                    if (lastJobCount > 0)
                        _sleepCycle = 1;
                    else
                        _sleepCycle++;

                    if (_sleepCycle == 1)
                    {
                        // First time -> always restart without delay
                        _log.LogInformation($"Requesting restart without delay, cycle {_sleepCycle}");
                        RaiseRequireRestart($"Sleep-Time-Restart {_sleepCycle}");
                    }
                    else if (lastJobCount == 0 && _sleepCycle == 2)
                    {
                        // No jobs, second time -> delay, restart
                        _log.LogInformation($"Sleeping {_sleepTime}ms and requesting restart, cycle {_sleepCycle}");
                        RaiseRequireRestart($"Sleep-Time-Restart {_sleepCycle}");
                        Thread.Sleep(_sleepTime);
                    }
                    else if (lastJobCount == 0)
                    {
                        // No jobs, 3+ time, go idle
                        _log.LogInformation($"Sleep cycle done resetting cycle.");
                        _sleepCycle = 0;
                    }
                }
            }
            catch (TimeDriftException ex)
            {
                _serviceDead = true;
                _log.LogError($"Time drift detected, shutting down!\n{ex.Message}");

                Task.Run(() => Environment.Exit(-1));
            }
        }

        private void ArchiveJobs()
        {
            var archiveWatch = Stopwatch.StartNew();
            try
            {
                var archivedCount = 0;
                var archivedClosedByCount = 0;

                // Part 1: Archive jobs that were not closed due to other jobs
                using (var db = GetDb())
                {
                    archivedCount = db.ArchiveFinishedSyncJobs();
                }

                archiveWatch.Stop();
                _log.LogInformation($"Archived {archivedCount} jobs in {SpecialFormat.FromMilliseconds((int)archiveWatch.Elapsed.TotalMilliseconds)}.");

                // Part 2: Archive jobs that were closed by other jobs,
                //         only if no normal jobs were archived
                if (archivedCount == 0)
                {
                    archiveWatch.Reset();
                    archiveWatch.Start();
                    using (var db = GetDb())
                    {
                        archivedClosedByCount = db.ArchiveFinishedSyncJobsClosedByOtherJobs();
                    }

                    archiveWatch.Stop();
                    _log.LogInformation($"Archived (Part 2) {archivedCount} jobs in {SpecialFormat.FromMilliseconds((int)archiveWatch.Elapsed.TotalMilliseconds)}.");
                }

                if (archivedCount > 0 || archivedClosedByCount > 0)
                    RaiseRequireRestart("Archive finished SyncJobs");
            }
            catch (Exception ex)
            {
                archiveWatch.Stop();

                if (ex.GetType().Equals(typeof(NpgsqlException)) && (ex as NpgsqlException).ErrorCode == -2147467259)
                {
                    _log.LogWarning($"Timeout while archiving. Elapsed time: {SpecialFormat.FromMilliseconds((int)archiveWatch.Elapsed.TotalMilliseconds)}.");
                    //RaiseRequireRestart($"Archiving timed out after {SpecialFormat.FromMilliseconds((int)archiveWatch.Elapsed.TotalMilliseconds)}. Requesting restart to try again.");
                }
                else
                {
                    _log.LogError($"Failed to archive: {ex.ToString()} elapsed time: {SpecialFormat.FromMilliseconds((int)archiveWatch.Elapsed.TotalMilliseconds)}.");
                }
            }
        }

        private void CheckInProgress(IEnumerable<SyncJob> jobs)
        {
            var inProgressJobCount = jobs
                .Where(j => j.Job_State == SosyncState.InProgress.Value)
                .Count();

            if (inProgressJobCount > 0)
            {
                var body = $"{Configuration.Instance}: sosync2 found {inProgressJobCount} jobs with status " +
                    $"\"{SosyncState.InProgress.Value}\" - jobs will be treated like " +
                    $"status \"{SosyncState.New.Value}\" but with highest priority.\n\n" +
                    $"max_threads: {Configuration.Max_Threads}\n" +
                    $"job_package_size: {Configuration.Job_Package_Size}";

                _log.LogWarning(body);

                _mail.Send(
                    "michael.karrer@datadialog.net,martin.kaip@datadialog.net",
                    $"{Configuration.Instance} sosync2 - Jobs \"inprogress\" found",
                    body);
            }
        }
        private void ThrowOnMismatchedServerTimes(int reCheckTimeMin)
        {
            // No previous check, or last check older than reCheckTimeMin
            if (_timeSvc.LastDriftCheck == null || (DateTime.UtcNow - _timeSvc.LastDriftCheck.Value).Minutes >= reCheckTimeMin)
            {
                _log.LogInformation($"Checking time drift...");
                _timeSvc.ThrowOnTimeDrift();
            }
            else
            {
                _log.LogInformation($"Time drift was checked within last {reCheckTimeMin} minutes. Skipping check.");
            }
        }

        private void JobThread(ref ConcurrentQueue<SyncJob> jobs, DateTime loadTimeUTC, Guid threadNumber)
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

                    if (jobs.TryDequeue(out threadJob))
                    {
                        _log.LogInformation($"Thread {threadNumber} fetched a job from memory queue");
                    }

                    // Process job
                    if (threadJob != null)
                    {
                        _log.LogInformation($"Thread {threadNumber} processing the job {threadJob.ID}");
                        threadJob.Job_Log += $"GetNextOpenJob: n/a ms (comes from thread queue)\n";
                        ProcessJob(threadJob, DateTime.UtcNow);
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
            finally
            {
                // Before finishing the thread, clean up OdooService instance
                _log.LogInformation($"Thread {threadNumber} disposing of OdooService class.");
                var odooSvc = _svc.GetService<OdooService>();
                odooSvc.Client.Dispose();
                odooSvc.Client = null;
            }

            _log.LogInformation($"Thread {threadNumber} clean exit");
        }

        private void ProcessJob(SyncJob job, DateTime loadTimeUTC)
        {
            _log.LogInformation($"{nameof(ProcessJob)}: Entry (id  {job.ID})");

            using (var dataService = GetDb())
            {
                try
                {
                    _log.LogInformation($"{nameof(ProcessJob)}: Update run count (id  {job.ID})");
                    UpdateJobRunCount(dataService, job);

                    _log.LogInformation($"{nameof(ProcessJob)}: Update job start (id  {job.ID})");
                    UpdateJobStart(dataService, job, loadTimeUTC);

                    _log.LogInformation($"{nameof(ProcessJob)}: Activator.CreateInstance for SyncFlow (id  {job.ID})");

                    // Get the flow for the job source model, and start it
                    var svc = _svc.GetService<SyncServiceCollection>();
                    svc.Log = _log;

                    var constructorParams = new object[] { svc };
                    using (SyncFlow flow = (SyncFlow)Activator.CreateInstance(_flowService.GetFlow(job.Job_Source_Type, job.Job_Source_Model), constructorParams))
                    {
                        bool requireRestart = false;
                        string restartReason = "";

                        _log.LogInformation($"{nameof(ProcessJob)}: Starting SyncFlow (id  {job.ID})");

                        flow.Start(_flowService, job, loadTimeUTC, ref requireRestart, ref restartReason);

                        _log.LogInformation($"{nameof(ProcessJob)}: Closing previous jobs (id  {job.ID})");

                        if (new string[] { "done", "error", "skipped" }.Contains((job.Job_State ?? "").ToLower()))
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
                        _log.LogInformation($"{nameof(ProcessJob)}: Check and handle throttling (id  {job.ID})");
                        ThrottleJobProcess(loadTimeUTC);

                        // Stop processing the queue if cancellation was requested
                        _log.LogInformation($"{nameof(ProcessJob)}: Check for cancellation (id  {job.ID})");
                        if (CancellationToken.IsCancellationRequested)
                            RaiseCancelling();
                    }

                    _log.LogInformation($"{nameof(ProcessJob)}: Success SyncFlow (id  {job.ID})");
                }
                catch (Exception ex)
                {
                    UpdateJobError(dataService, job, SosyncError.Unknown, ex.ToString(), useErrorRetry: false);
                }
            }

            _log.LogInformation($"{nameof(ProcessJob)}: Done (id  {job.ID})");
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
                    _log.LogInformation($"Job time ({consumedTimeMs}ms) exceeded throttle time ({Configuration.Throttle_ms}ms), continuing at full speed.");
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
                _log.LogInformation($"Loaded {result.Count} jobs: {nameof(GetNextOpenJob)} elapsed: {s.ElapsedMilliseconds} ms");

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
            _log.LogInformation($"Updating job {job.ID}: job start");

            job.Job_State = SosyncState.InProgress.Value;
            job.Job_Start = loadTimeUTC;
            job.Write_Date = DateTime.UtcNow;

            db.UpdateJob(job);
        }

        protected void UpdateJobRunCount(DataService db, SyncJob job)
        {
            _log.LogInformation($"Updating job {job.ID}: run_count");

            job.Job_Run_Count += 1;
            job.Write_Date = DateTime.UtcNow;

            db.UpdateJob(job);
        }

        private void UpdateJobError(DataService db, SyncJob job, SosyncError sosyncError, string message, bool useErrorRetry = true)
        {
            JobHelper.SetJobError(job, sosyncError, message, useErrorRetry);
            db.UpdateJob(job);
        }
        #endregion
    }
}