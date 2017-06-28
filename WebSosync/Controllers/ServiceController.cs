using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebSosync.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WebSosync.Services;
using WebSosync.Common.Interfaces;
using Syncer;

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

        //// GET api/values/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}
        #endregion
    }
}
