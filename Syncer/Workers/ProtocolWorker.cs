using dadi_data;
using Microsoft.Extensions.DependencyInjection;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Linq;
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
        #endregion

        #region Constructors
        public ProtocolWorker(IServiceProvider svc, SosyncOptions options, FlowService flowManager)
            : base(options)
        {
            _svc = svc;
            _odoo = _svc.GetService<OdooService>();
            _flowManager = flowManager;
        }
        #endregion

        #region Methods
        public override void Start()
        {
            //var id = _odoo.Client.SearchModelByField<SyncJob, int>("sosync.job", x => x.Job_ID, 100).SingleOrDefault();

            //if (id > 0)
            //{
            //    var result = _odoo.Client.GetModel<SyncJob>("sosync.job", id);
            //}

            var f = (SyncFlow)_svc.GetService(_flowManager.GetFlow("res.partner"));
        }
        #endregion
    }
}
