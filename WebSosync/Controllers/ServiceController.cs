using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Models;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Collections.Generic;
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

        [HttpGet("check")]
        public async Task<IActionResult> CheckAsync([FromQuery]string model, [FromQuery]string id, [FromQuery]string fk)
        {
            try
            {
                // Validate
                var badRequest = false;
                var messages = new List<string>();

                if (string.IsNullOrEmpty(model))
                {
                    badRequest = true;
                    messages.Add("Model required.");
                }

                int idValue = 0;
                if (string.IsNullOrEmpty(id))
                {
                    badRequest = true;
                    messages.Add("ID required.");
                }
                else if (!string.IsNullOrEmpty(id) && !int.TryParse(id, out idValue) || idValue == 0)
                {
                    badRequest = true;
                    messages.Add("ID must be an integer value greater than zero.");
                }

                int fkValue = 0;
                if (!string.IsNullOrEmpty(fk) && !int.TryParse(fk, out fkValue))
                {
                    badRequest = true;
                    messages.Add("FK (foreign key) must be an integer value.");
                }

                if (badRequest)
                    return new BadRequestObjectResult(string.Join(" ", messages));

                // Process
                var result = await _flowCheckService.GetModelState(
                    model,
                    idValue,
                    fkValue == 0 ? null : (int?)fkValue);

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
