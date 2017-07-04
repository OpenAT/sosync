using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Odoo;
using Syncer.Workers;
using System;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Enumerations;
using WebSosync.Models;
using WebSosync.Services;

namespace WebSosync.Controllers
{
    // job
    [Route("[controller]")]
    public class JobController : Controller
    {
        #region Members
        private DataService _db;
        private IBackgroundJob<SyncWorker> _job;
        private ILogger<JobController> _log;
        #endregion
        
        #region Constructors
        public JobController(ILogger<JobController> log, DataService db, IBackgroundJob<SyncWorker> job)
        {
            _log = log;
            _db = db;
            _job = job;
        }
        #endregion

        #region Methods
        [HttpGet("info/{id}")]
        public IActionResult Get([FromRoute]int id)
        {
            var result = _db.GetJob(id);

#warning TODO: It's a bad practice to return internal objects without DTOs, probably discuss later
            if (result != null)
                return new OkObjectResult(result);
            else
                return new BadRequestObjectResult(new JobResultDto()
                {
                    ErrorCode = (int)JobErrorCode.DataError,
                    ErrorText = JobErrorCode.DataError.ToString(),
                    ErrorDetail = new string[] { "Job not found." }
                });
        }

        [HttpGet("list")]
        public IActionResult GetAll()
        {
#warning TODO: Bad practice and returning everything unpaged... fix some time!
            var result = _db.GetJobs(false);
            return new OkObjectResult(result);
        }

        // job/create
        [HttpGet("create")]
        public IActionResult Get([FromQuery]SyncJobDto jobDto, [FromServices]SosyncOptions config, [FromServices]RequestValidator<SyncJobDto> validator)
        {
            JobResultDto result = new JobResultDto();
            validator.Configure(Request, ModelState);

            validator.AddCustomCheck(x => x.SourceSystem, val =>
            {
                if (val != "fs" && val != "fso")
                    return "source_system can only be 'fs' or 'fso'";

                return "";
            });

            validator.Validate();

            result.ErrorCode = (int)validator.ErrorCode;
            result.ErrorText = validator.ErrorCode.ToString();
            result.ErrorDetail = validator.Errors.Values;
            
            // Only attempt to store the job, if validation was successful
            if (validator.ErrorCode == JobErrorCode.None)
            {
                // Create a full SyncJob object from the transfer object
                var job = Mapper.Map<SyncJobDto, SyncJob>(jobDto);

                // Defaults
                job.State = SosyncState.New;
                job.Fetched = DateTime.Now.ToUniversalTime();

                // Create the sync job, get it's ID into the result and start the job thread
                _db.CreateJob(job);
                result.JobID = job.Job_ID;
                _job.Start();

                try
                {
                    var client = new OdooClient($"http://{config.Online_Host}/xmlrpc/2/", config.Instance);
                    client.Authenticate(config.Online_Sosync_User, config.Online_Sosync_PW);
                    int odooId = client.CreateModel<SyncJob>("sosync.job", job);

                    if (job.Source_System == SosyncSystem.FSOnline)
                    {
                        job.Job_Fso_ID = odooId;
                        _db.UpdateJob(job, x => x.Job_Fso_ID);
                    }
                    else
                    {
                        job.Job_Fs_ID = odooId;
                        _db.UpdateJob(job, x => x.Job_Fs_ID);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.ToString());
                }

                // Just return empty OK result
                return new OkObjectResult(result);
            }
            else
            {
                return new BadRequestObjectResult(result);
            }
        }
        #endregion
    }
}
