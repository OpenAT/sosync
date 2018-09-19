﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Syncer.Models;
using Syncer.Services;
using Syncer.Workers;
using System;
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
        private IHostService _hostService;
        private ILogger<ServiceController> _log;
        #endregion

        #region Constructors
        public ServiceController(
            IBackgroundJob<SyncWorker> syncWorkerJob,
            IHostService hostService,
            ILogger<ServiceController> logger)
        {
            _syncWorkerJob = syncWorkerJob;
            _hostService = hostService;
            _log = logger;
        }
        #endregion

        #region Methods
        // GET service/status
        [HttpGet("status")]
        public IActionResult Status([FromServices]DataService db)
        {
            var result = new SosyncStatusDto();

            result.JobWorker.Status = (int)_syncWorkerJob.Status;
            result.JobWorker.StatusText = _syncWorkerJob.Status.ToString();

            return new OkObjectResult(result);
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
        #endregion
    }
}
