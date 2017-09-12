using dadi_data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            _log.LogInformation("Protocol worker START called.");
        }
        #endregion
    }
}
