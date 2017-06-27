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

namespace WebSosync.Controllers
{
    // job
    [Route("[controller]")]
    public class JobController : Controller
    {
        #region Members
        private DataService _db;
        private IBackgroundJob _job;
        #endregion
        
        #region Constructors
        public JobController(DataService db, IBackgroundJob job)
        {
            _db = db;
            _job = job;
        }
        #endregion

        #region Methods
        // job/create
        [HttpGet("create")]
        public IActionResult Get([FromQuery]SyncJobDto jobDto)
        {
            JobResultDto result = new JobResultDto();
            var resultDetails = new List<string>();

            // If MVC attribute based validation renders the
            // model invalid, treat that as interface error
            if (!ModelState.IsValid)
                result.ErrorCode = JobErrorCode.InterfaceError;

            // Add custom errors, aka data errors, to the model state
            var validator = new JobDtoValidator();
            var dataErrors = validator.ValidateCreation(jobDto);
            foreach (var de in dataErrors)
                ModelState.AddModelError(de.Key, de.Value);

            // If there were validation errors, and the error code is still none, set it to data error
            if (dataErrors.Count > 0 && result.ErrorCode == JobErrorCode.None)
                result.ErrorCode = JobErrorCode.DataError;

            // All validation is done, prepare the result
            result.ErrorText = result.ErrorCode.ToString();
            resultDetails.AddRange(ModelState.SelectMany(x => x.Value.Errors).Select(x => x.ErrorMessage));
            result.ErrorDetail = resultDetails;

            // Only attempt to store the job, if validation was successful
            if (result.ErrorCode == JobErrorCode.None)
            {
                try
                {
                    // Create a full SyncJob object from the transfer object
                    var job = Mapper.Map<SyncJobDto, SyncJob>(jobDto);

                    // Defaults
                    job.State = SosyncState.New;
                    job.Fetched = DateTime.Now.ToUniversalTime();

                    // Create the sync job and start the job thread
                    _db.CreateJob(job);

#warning TODO: Get the ID of the created job and set it to the result here
                    result.JobID = -1;

                    _job.Start();

                    // Just return empty OK result
                    return new OkObjectResult(result);
                }
                catch (Exception ex)
                {
                    // Return error without stack trace
                    return new BadRequestObjectResult(new JobResultDto() { });
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
