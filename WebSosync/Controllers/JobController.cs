using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Odoo;
using Syncer.Services;
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
        #region Constructors
        public JobController()
        {
        }
        #endregion

        #region Methods
        [HttpGet("info/{id}")]
        public IActionResult Get([FromServices]DataService db, [FromRoute]int id)
        {
            var result = db.GetJob(id);

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
        public IActionResult GetAll([FromServices]DataService db)
        {
#warning TODO: Bad practice and returning everything unpaged... fix some time!
            var result = db.GetJobs(false);
            return new OkObjectResult(result);
        }

        // job/create
        [HttpGet("create")]
        public IActionResult Get(
            [FromServices]SosyncOptions config,
            [FromServices]RequestValidator<SyncJobDto> validator,
            [FromServices]OdooService odoo,
            [FromServices]DataService db,
            [FromServices]IBackgroundJob<SyncWorker> syncJob,
            [FromServices]ILogger<JobController> log,
            [FromQuery]SyncJobDto jobDto)
        {
            JobResultDto result = new JobResultDto();
            validator.Configure(Request, ModelState);

            validator.AddCustomCheck(x => x.JobSourceSystem, val =>
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
                job.Job_State = SosyncState.New;
                job.Job_Fetched = DateTime.UtcNow;

                // Create the sync job, get it's ID into the result and start the job thread
                db.CreateJob(job);
                result.JobID = job.Job_ID;

                // Try to push the job to Odoo
                try
                {
                    int odooId = odoo.Client.CreateModel<SyncJob>("sosync.job", job);
                    job.Job_Fso_ID = odooId;
                    db.UpdateJob(job, x => x.Job_Fso_ID);
                }
                catch (Exception ex)
                {
                    log.LogError(ex.ToString());
                }

                // Start the sync background job
                syncJob.Start();

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
