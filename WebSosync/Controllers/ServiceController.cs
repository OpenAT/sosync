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
        private const int _maxActiveSeconds = 3600 * 3;

        private IBackgroundJob<SyncWorker> _syncWorkerJob;
        private ILogger<ServiceController> _log;
        private DataService _db;
        private GitService _git;
        private FlowCheckService _flowCheckService;
        private IThreadSettings _threadSettings;
        #endregion

        #region Constructors
        public ServiceController(
            IBackgroundJob<SyncWorker> syncWorkerJob,
            ILogger<ServiceController> logger,
            DataService db,
            GitService git,
            FlowCheckService flowCheckService,
            IThreadSettings threadSettings)
        {
            _syncWorkerJob = syncWorkerJob;
            _log = logger;
            _db = db;
            _git = git;
            _flowCheckService = flowCheckService;
            _threadSettings = threadSettings;
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
            return new OkResult();
        }

        [HttpGet("processjobs")]
        public IActionResult ProcessJobs([FromServices]IBackgroundJob<SyncWorker> syncJob)
        {
            syncJob.Start();
            return new OkResult();
        }

        [HttpGet("quick-check")]
        public async Task<IActionResult> CheckAsync([FromQuery]string model, [FromQuery]string id, [FromQuery]string fk)
        {
            try
            {
                // Validate
                var messages = RequestValidator
                    .ValidateQuickCheck(model, id, fk);

                if (messages.Count > 0)
                    return new BadRequestObjectResult(string.Join(" ", messages));

                int idValue = int.Parse(id);
                int fkValue = 0;
                int.TryParse(fk, out fkValue);

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

        [HttpGet("threads")]
        public IThreadSettings GetThreads()
        {
            return _threadSettings;
        }

        [HttpPost("threads")]
        public IActionResult SetThreads([FromServices] IBackgroundJob<SyncWorker> syncWorker, [FromBody]ThreadSettingsDto newSettings)
        {
            // Guards

            if (newSettings is null)
                return BadRequest("Missing settings.");

            if ((newSettings.Threads ?? 0) <= 0)
                return BadRequest($"{nameof(newSettings.Threads)} is required and must be greater than zero.");

            if ((newSettings.Threads ?? 0) > 30)
                return BadRequest($"Max value for {nameof(newSettings.Threads)} is 30.");

            if (newSettings.Threads != null && (newSettings.ActiveSeconds ?? 0) <= 0)
                return BadRequest($"{nameof(newSettings.ActiveSeconds)} is required and must be greater than zero.");

            if (newSettings.Threads != null && (newSettings.PackageSize ?? 0) <= 0)
                return BadRequest($"{nameof(newSettings.PackageSize)} is required and must be greater than zero.");

            if (newSettings.Threads != null && (newSettings.PackageSize ?? 0) > 200)
                return BadRequest($"Max value for {nameof(newSettings.PackageSize)} is 200.");

            if (newSettings.Threads is null && newSettings.ActiveSeconds != null)
                return BadRequest($"Cannot set {nameof(newSettings.ActiveSeconds)} when resetting threads.");

            // Reset to configuration
            string msg;

            if (newSettings.Threads is null && newSettings.ActiveSeconds is null)
            {
                _threadSettings.TargetMaxThreads = null;
                _threadSettings.TargetPackageSize = null;
                _threadSettings.TargetMaxThreadsEnd = null;

                syncWorker.Start();

                msg = "Threads reset to configuration.";
                _log.LogInformation(msg);
                return Ok(msg);
            }

            // Force threads

            if (newSettings.ActiveSeconds > _maxActiveSeconds)
                return BadRequest($"Max value for {newSettings.ActiveSeconds} is {_maxActiveSeconds}.");

            _threadSettings.TargetMaxThreads = newSettings.Threads;
            _threadSettings.TargetPackageSize = newSettings.PackageSize;
            _threadSettings.TargetMaxThreadsEnd = DateTime.Now
                + TimeSpan.FromSeconds(newSettings.ActiveSeconds.Value);

            syncWorker.Start();

            msg = $"Forcing {_threadSettings.TargetMaxThreads} threads with package size {_threadSettings.TargetPackageSize} until {_threadSettings.TargetMaxThreadsEnd:yyyy-mm-dd HH:mm:ss}";
            _log.LogInformation(msg);
            return Ok(msg);
        }
        #endregion
    }
}
