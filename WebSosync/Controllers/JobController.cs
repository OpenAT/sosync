﻿using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Odoo;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        [HttpPost("create")]
        public IActionResult Post([FromServices]IServiceProvider services, [FromBody]Dictionary<string, object> data)
        {
            if (data == null)
                return new BadRequestResult();

            return HandleCreateJob(services, data);
        }

        [HttpGet("create")]
        public IActionResult Get([FromServices]IServiceProvider services, [FromBody]Dictionary<string, object> data)
        {
            return HandleCreateJob(services, data);
        }

        private IActionResult HandleCreateJob(IServiceProvider services, Dictionary<string, object> data)
        {
            var result = new JobResultDto()
            {
                ErrorCode = (int)JobErrorCode.None,
                ErrorText = JobErrorCode.None.ToString(),
                ErrorDetail = new Dictionary<string, string>()
            };

            string dateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";
            
            if (!RequestValidator.ValidateInterface(result, data))
                return new BadRequestObjectResult(result);

            if (!RequestValidator.ValidateData(result, data, dateFormat))
                return new BadRequestObjectResult(result);

            var config = (SosyncOptions)services.GetService(typeof(SosyncOptions));
            var odoo = (OdooService)services.GetService(typeof(OdooService));
            var syncJob = (IBackgroundJob<SyncWorker>)services.GetService(typeof(IBackgroundJob<SyncWorker>));
            var log = (ILogger<JobController>)services.GetService(typeof(ILogger<JobController>));

            using (var db = (DataService)services.GetService(typeof(DataService)))
            {
                var job = new SyncJob()
                {
                    Job_Date = DateTime.Parse((string)data["job_date"], CultureInfo.InvariantCulture),
                    Job_Source_System = (string)data["job_source_system"],
                    Job_Source_Model = (string)data["job_source_model"],
                    Job_Source_Record_ID = (int)(long)data["job_source_record_id"]
                };

                if (data.ContainsKey("job_source_sosync_write_date"))
                    job.Job_Source_Sosync_Write_Date = (DateTime)data["job_source_sosync_write_date"];

                if (data.ContainsKey("job_source_fields"))
                {
                    if (data["job_source_fields"].GetType() == typeof(string))
                        job.Job_Source_Fields = new JValue((string)data["job_source_fields"]).ToString();
                    else if (data["job_source_fields"].GetType() == typeof(JValue))
                        job.Job_Source_Fields = ((JValue)data["job_source_fields"]).ToString();
                    else if (data["job_source_fields"].GetType() == typeof(string))
                        job.Job_Source_Fields = ((JObject)data["job_source_fields"]).ToString();
                    else if (data["job_source_fields"].GetType() == typeof(bool))
                        job.Job_Source_Fields = null;
                    else
                        throw new Exception("Content of job_source_fields was not recognized.");
                }

                job.Job_State = SosyncState.New;
                job.Job_Fetched = DateTime.UtcNow;

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
            }

            return new OkObjectResult(result);
        }
        #endregion
    }
}
