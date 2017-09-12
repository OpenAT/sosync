using dadi_data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Diagnostics;
using System.Linq;
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
            var count = 0;
            var s = new Stopwatch();
            s.Start();

            var job = GetNextJobToSync();
            while (job != null)
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

                job = GetNextJobToSync();
            }

            s.Stop();
            _log.LogInformation($"Sent {count} jobs to [fso] in {SpecialFormat.FromMilliseconds((int)s.ElapsedMilliseconds)}.");
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
