using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using System;
using System.Threading;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Workers
{
    public class ProtocolWorker : WorkerBase
    {
        #region Members
        private IServiceProvider _svc;
        private OdooService _odoo;
        private FlowService _flowManager;
        private ILogger<ProtocolWorker> _log;
        #endregion

        #region Constructors
        public ProtocolWorker(
            IServiceProvider svc,
            SosyncOptions options,
            FlowService flowManager,
            ILogger<ProtocolWorker> logger)
            : base(options)
        {
            _svc = svc;
            _odoo = _svc.GetService<OdooService>();
            _flowManager = flowManager;
            _log = logger;
        }
        #endregion

        #region Methods
        public override void Start()
        {
            var throttle = Configuration.Protocol_Throttle_ms;

            var count = 0;
            var protocolStart = DateTime.UtcNow;
            var syncStart = DateTime.UtcNow;

            var job = GetNextJobToSync();
            while (job != null && !CancellationToken.IsCancellationRequested)
            {
                _log.LogInformation($"Syncing job_id ({job.Job_ID}) with [fso]");

                int? odooID = _odoo.SendSyncJob(job);

                if (odooID.HasValue)
                {
                    job.Job_Fso_ID = odooID.Value;
                    UpdateJobFsoID(job);
                }

                UpdateJobSyncInfo(job);
                count++;

                if (throttle > 0)
                {
                    var consumedTimeMs = (int)(DateTime.UtcNow - syncStart).TotalMilliseconds;
                    var remainingTimeMs = throttle - consumedTimeMs;

                    if (remainingTimeMs > 0)
                    {
                        _log.LogInformation($"Throttle set to {throttle}ms, updating job in [fso] took {consumedTimeMs}ms, sleeping {remainingTimeMs}ms");
                        Thread.Sleep(remainingTimeMs);
                    }
                    else
                    {
                        _log.LogDebug($"Updatinug job in [fso] ({consumedTimeMs}ms) exceeded throttle time ({throttle}ms), continuing at full speed.");
                    }
                }

                // Stop processing the queue if cancellation was requested
                if (CancellationToken.IsCancellationRequested)
                {
                    // Raise the cancelling event
                    RaiseCancelling();
                    // Clean up here, if necessary
                }

                syncStart = DateTime.UtcNow;
                job = GetNextJobToSync();
            }

            var elapsedMs = (int)(DateTime.UtcNow - protocolStart).TotalMilliseconds;
            _log.LogInformation($"Sent {count} jobs to [fso] in {SpecialFormat.FromMilliseconds(elapsedMs)}, throttle at {SpecialFormat.FromMilliseconds(throttle)}.");
        }

        private SyncJob GetNextJobToSync()
        {
            using (var db = _svc.GetService<DataService>())
            {
                return db.GetJobToSync();
            }
        }

        private void UpdateJobFsoID(SyncJob job)
        {
            using (var db = _svc.GetService<DataService>())
            {
                db.UpdateJob(job, x => x.Job_Fso_ID);
            }
        }

        private void UpdateJobSyncInfo(SyncJob job)
        {
            using (var db = _svc.GetService<DataService>())
            {
                job.Job_To_FSO_Sync_Date = DateTime.UtcNow;
                job.Job_To_FSO_Sync_Version = job.Job_Last_Change;
                db.UpdateJob(job);
            }
        }
        #endregion
    }
}
