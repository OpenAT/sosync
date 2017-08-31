using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Odoo.Models;
using Syncer.Services;
using Syncer.Workers;
using System;
using WebSosync.Common.Interfaces;
using WebSosync.Interfaces;
using WebSosync.Models;
using WebSosync.Services;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class ServiceController : Controller
    {
        #region Members
        private IBackgroundJob<SyncWorker> _syncWorkerJob;
        private IBackgroundJob<ProtocolWorker> _protocolWorkerJob;
        private IHostService _hostService;
        private ILogger<ServiceController> _log;
        #endregion

        #region Constructors
        public ServiceController(
            IBackgroundJob<SyncWorker> syncWorkerJob,
            IBackgroundJob<ProtocolWorker> protocolWorkerJob,
            IHostService hostService,
            ILogger<ServiceController> logger)
        {
            _syncWorkerJob = syncWorkerJob;
            _protocolWorkerJob = protocolWorkerJob;
            _hostService = hostService;
            _log = logger;
        }
        #endregion

        #region Methods
        // GET service/status
        [HttpGet("status")]
        public IActionResult Get()
        {
            var result = new SosyncStatusDto();

            result.JobWorker.Status = (int)_syncWorkerJob.Status;
            result.JobWorker.StatusText = _syncWorkerJob.Status.ToString();

            result.ProtocolWorker.Status = (int)_protocolWorkerJob.Status;
            result.ProtocolWorker.StatusText = _protocolWorkerJob.Status.ToString();

            return new OkObjectResult(result);
        }

        // service/protocol
        [HttpGet("protocol")]
        public IActionResult ProtocolStart()
        {
            _protocolWorkerJob.Start();
            return new OkResult();
        }

        // service/version
        [HttpGet("version")]
        [Produces(typeof(string))]
        public IActionResult Version([FromServices]GitService git)
        {
            var result = "";

            try
            {
                result = git.GetCommitId();
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _log.LogError(ex.ToString());
                return new BadRequestObjectResult("Could not read version.");
            }
        }

        public IActionResult Debug([FromServices]OdooService odoo)
        {
            var result = odoo.Client.GetModel<resPartnerBpk>("res.partner.bpk", 1);
            return new OkObjectResult(result);
        }

        //// GET api/values/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}
        #endregion
    }
}
