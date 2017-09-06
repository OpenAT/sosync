﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Linq;
using System.Threading;
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
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="SyncWorker"/> class.
        /// </summary>
        /// <param name="options">Options to be used for data connections etc.</param>
        public SyncWorker(IServiceProvider svc, SosyncOptions options, FlowService flowManager, ILogger<SyncWorker> logger, OdooService odoo)
            : base(options)
        {
            _svc = svc;
            _flowManager = flowManager;
            _log = logger;
            _odoo = odoo;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
            // Get only the first open job and its hierarchy,
            // and build the tree in memory
            var loadTimeUTC = DateTime.UtcNow;
            var job = GetNextOpenJob();

            while (job != null)
            {
                try
                {
                    // Get the flow for the job source model, and start it
                    using (SyncFlow flow = (SyncFlow)_svc.GetService(_flowManager.GetFlow(job.Job_Source_Model)))
                    {
                        bool requireRestart = false;
                        flow.Start(_flowManager, job, loadTimeUTC, ref requireRestart);

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
                catch(Exception ex)
                {
                    _log.LogError(ex.Message);
                    UpdateJobError(job, ex.Message);
                }

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

        private void UpdateJobError(SyncJob job, string message)
        {
            using (var db = _svc.GetService<DataService>())
            {
                job.Job_State = SosyncState.Error;
                job.Job_Log = message;
                job.Job_Last_Change = DateTime.UtcNow;
                db.UpdateJob(job);

                int? fsoID = _odoo.SendSyncJob(job);

                if (fsoID != null)
                {
                    job.Job_Fso_ID = fsoID;
                    db.UpdateJob(job);
                }
            }
        }
        #endregion
    }
}