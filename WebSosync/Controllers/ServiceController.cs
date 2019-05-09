using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Models;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Models;
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
        private ILogger<ServiceController> _log;
        private DataService _db;
        private GitService _git;
        private FlowCheckService _flowCheckService;
        #endregion

        #region Constructors
        public ServiceController(
            IBackgroundJob<SyncWorker> syncWorkerJob,
            ILogger<ServiceController> logger,
            DataService db,
            GitService git,
            FlowCheckService flowCheckService)
        {
            _syncWorkerJob = syncWorkerJob;
            _log = logger;
            _db = db;
            _git = git;
            _flowCheckService = flowCheckService;
        }
        #endregion

        #region Methods
        // GET service/status
        [HttpGet("status")]
        public IActionResult Status()
        {
            var result = new SosyncStatusDto();

            result.JobWorker.Status = (int)_syncWorkerJob.Status;
            result.JobWorker.StatusText = _syncWorkerJob.Status.ToString();

            ThreadPool.GetAvailableThreads(out var workerThreads, out var ioThreads);
            result.ThreadPool.WorkerThreads = workerThreads;
            result.ThreadPool.IOThreads = ioThreads;

            return new OkObjectResult(result);
        }

        // service/version
        [HttpGet("version")]
        [Produces(typeof(string))]
        public IActionResult Version()
        {
            var result = "";

            try
            {
                result = _git.GetCommitId();
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _log.LogError(ex.ToString());
                return new BadRequestObjectResult("Could not read version.");
            }
        }

        [HttpGet("drift")]
        public IActionResult Drift([FromServices]TimeService timeSvc, [FromServices]SosyncOptions config)
        {
            var result = new TimeDriftDto()
            {
                Drift = timeSvc.GetTimeDrift(),
                Tolerance = config.Max_Time_Drift_ms,
                Unit = "millisecond"
            };

            return new OkObjectResult(result);
        }

        [HttpGet("forcedriftcheck")]
        public IActionResult ForceDriftCheck([FromServices]TimeService timeSvc)
        {
            timeSvc.LastDriftCheck = null;
            timeSvc.DriftLockUntil = null;
            return new OkResult();
        }

        [HttpGet("processjobs")]
        public IActionResult ProcessJobs([FromServices]IBackgroundJob<SyncWorker> syncJob)
        {
            syncJob.Start();
            return new OkResult();
        }

        [HttpGet("check/{modelName}/{id}/{foreignId?}")]
        public async Task<IActionResult> CheckAsync(string modelName, int id, int? foreignId)
        {
            try
            {
                var result = await _flowCheckService.GetModelState(
                    modelName,
                    id,
                    foreignId);

                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }
        #endregion
    }
}
