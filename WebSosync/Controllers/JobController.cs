﻿using AutoMapper;
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

            // Add custom errors, aka data errors, to the model state
            var validator = new RequestValidator<SyncJobDto>(Request, ModelState);

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
