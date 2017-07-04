using dadi_data;
using Microsoft.Extensions.DependencyInjection;
using Syncer.Services;
using System;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Workers
{
    public class ProtocolWorker : WorkerBase
    {
        #region Members
        private IServiceProvider _svc;
        private OdooService _odoo;
        #endregion

        #region Constructors
        public ProtocolWorker(IServiceProvider svc, SosyncOptions options)
            : base(options)
        {
            _svc = svc;
            _odoo = _svc.GetService<OdooService>();
        }
        #endregion

        #region Methods
        public override void Start()
        {
            using (var db = _svc.GetService<DataService>())
            {
                var job = db.GetJob(79);

                job.State = SosyncState.New;

                _odoo.Client.UpdateModel<SyncJob>("sosync.job", job, job.Job_Fso_ID.Value);
            }
        }
        #endregion
    }
}
