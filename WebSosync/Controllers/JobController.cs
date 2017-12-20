using AutoMapper;
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
        #region Members
        private ILogger<JobController> _log;
        private IBackgroundJob<SyncWorker> _jobWorker;
        #endregion

        #region Constructors
        public JobController([FromServices]IBackgroundJob<SyncWorker> worker, [FromServices]ILogger<JobController> logger)
        {
            _jobWorker = worker;
            _log = logger;
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
        public IActionResult Get([FromServices]IServiceProvider services, [FromQuery]Dictionary<string, string> data)
        {
            var converted = new Dictionary<string, object>();

            foreach (var entry in data)
                converted.Add(entry.Key, entry.Value);

            return HandleCreateJob(services, converted);
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

            using (var db = (DataService)services.GetService(typeof(DataService)))
            {
                var job = new SyncJob()
                {
                    Job_Date = DateTime.Parse((string)data["job_date"], CultureInfo.InvariantCulture),
                    Job_Source_System = (string)data["job_source_system"],
                    Job_Source_Model = (string)data["job_source_model"],
                    Job_Source_Record_ID = Convert.ToInt32(data["job_source_record_id"]),
                    Job_Last_Change = DateTime.UtcNow
                };

                if (data.ContainsKey("job_source_sosync_write_date"))
                {
                    var val = data["job_source_sosync_write_date"];

                    if (val != null && val.GetType() == typeof(DateTime))
                        job.Job_Source_Sosync_Write_Date = (DateTime)data["job_source_sosync_write_date"];
                    else if (val != null && val.GetType() == typeof(string) && !string.IsNullOrEmpty((string)val))
                        job.Job_Source_Sosync_Write_Date = DateTime.Parse(
                            (string)data["job_source_sosync_write_date"],
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    else if (val != null && val.GetType() == typeof(bool) && (bool)val == false)
                        job.Job_Source_Sosync_Write_Date = null;
                    else
                        throw new Exception($"Unrecognized format ({val}) in field job_source_sosync_write_date.");
                }

                if (data.ContainsKey("job_source_fields"))
                {
                    if (data["job_source_fields"].GetType() == typeof(string))
                        job.Job_Source_Fields = new JValue((string)data["job_source_fields"]).ToString();
                    else if (data["job_source_fields"].GetType() == typeof(JObject))
                        job.Job_Source_Fields = ((JObject)data["job_source_fields"]).ToString();
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
                job.Job_To_FSO_Can_Sync = false;

                db.CreateJob(job);
                result.JobID = job.Job_ID;

                // Start the sync background job
                _jobWorker.Start();
            }

            return new OkObjectResult(result);
        }
        #endregion
    }
}
