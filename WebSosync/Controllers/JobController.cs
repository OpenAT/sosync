using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;
using WebSosync.Models;
using System.Reflection;
using System.Runtime.Serialization;
using WebSosync.Helpers;
using WebSosync.Services;
using WebSosync.Common.Interfaces;
using Syncer;

namespace WebSosync.Controllers
{
    // job
    [Route("[controller]")]
    public class JobController : Controller
    {
        #region Members
        private DataService _db;
        private IBackgroundJob<SyncWorker> _job;
        #endregion
        
        #region Constructors
        public JobController(DataService db, IBackgroundJob<SyncWorker> job)
        {
            _db = db;
            _job = job;
        }
        #endregion

        #region Methods
        // job/create
        [HttpGet("create")]
        public IActionResult Get([FromQuery]SyncJobDto jobDto, [FromServices]RequestValidator<SyncJobDto> validator)
        {
            JobResultDto result = new JobResultDto();
            validator.Configure(Request, ModelState);

            validator.AddCustomCheck("source_system", val =>
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
                try
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

                    // Just return empty OK result
                    return new OkObjectResult(result);
                }
                catch (Exception ex)
                {
                    result.ErrorCode = (int)JobErrorCode.DataError;
                    result.ErrorText = JobErrorCode.DataError.ToString();

                    var details = new List<string>(1);
                    details.Add(ex.Message);

                    result.ErrorDetail = details;

                    // Return error without stack trace
                    return new BadRequestObjectResult(result);
                }
            }
            else
            {
                return new BadRequestObjectResult(result);
            }
        }
        #endregion
    }
}
