using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using WebSosync.Common;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Constants;
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

            var result = ValidateDictionary(data);

            if (result.ErrorCode != (int)JobErrorCode.None)
                return new BadRequestObjectResult(result);

            var job = ParseJob(services, data);
            StoreJob(services, job);
            result.JobID = job.Job_ID;

            // Start the sync background job
            _jobWorker.Start();

            return new OkObjectResult(result);
        }

        [HttpPost("bulk-create")]
        public IActionResult Post([FromServices]IServiceProvider services, [FromBody]Dictionary<string, object>[] dictionaryList)
        {
            if (dictionaryList == null || dictionaryList.Length == 0)
                return new BadRequestResult();

            var isBadRequest = false;
            var result = new List<JobResultDto>();

            // Validate all dictionaries in batch
            foreach (var dictionary in dictionaryList)
            {
                var validationResult = ValidateDictionary(dictionary);

                if (validationResult.ErrorCode != (int)JobErrorCode.None)
                    isBadRequest = true;

                result.Add(validationResult);
            }

            // If no validation failed, create all jobs
            if (!isBadRequest)
            {
                for (int i = 0; i < dictionaryList.Length; i++)
                {
                    var job = ParseJob(services, dictionaryList[i]);
                    StoreJob(services, job);
                    result[i].JobID = job.Job_ID;
                }

                // Start the sync background job
                _jobWorker.Start();

                return new OkObjectResult(result);
            }

            return new BadRequestObjectResult(result);
        }

        private JobResultDto ValidateDictionary(Dictionary<string, object> data)
        {
            var result = new JobResultDto()
            {
                ErrorCode = (int)JobErrorCode.None,
                ErrorText = JobErrorCode.None.ToString(),
                ErrorDetail = new Dictionary<string, string>()
            };

            string dateFormat = "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";

            if (!RequestValidator.ValidateInterface(result, data))
                return result;

            if (!RequestValidator.ValidateData(result, data, dateFormat))
                return result;

            return result;
        }

        private SyncJob ParseJob(IServiceProvider services, Dictionary<string, object> data)
        {
            var job = new SyncJob()
            {
                Job_Date = DateTime.Parse((string)data["job_date"], CultureInfo.InvariantCulture),
                Job_Source_System = (string)data["job_source_system"],
                Job_Source_Model = (string)data["job_source_model"],
                Job_Source_Record_ID = Convert.ToInt32(data["job_source_record_id"]),
                Job_Last_Change = DateTime.UtcNow
            };

            if (data.ContainsKey("job_source_type")
                && data["job_source_type"] != null
                && data["job_source_type"].GetType() == typeof(string)
                && !string.IsNullOrEmpty((string)data["job_source_type"]))
            {
                job.Job_Source_Type = (string)data["job_source_type"];

                if (job.Job_Source_Type == SosyncJobSourceType.MergeInto)
                {
                    job.Job_Source_Merge_Into_Record_ID = Convert.ToInt32(data["job_source_merge_into_record_id"]);

                    if (data.ContainsKey("job_source_target_merge_into_record_id") && data["job_source_target_merge_into_record_id"] != null)
                        job.Job_Source_Target_Merge_Into_Record_ID = Convert.ToInt32(data["job_source_target_merge_into_record_id"]);
                }
            }

            if (data.ContainsKey("job_source_target_record_id") && data["job_source_target_record_id"] != null)
                job.Job_Source_Target_Record_ID = Convert.ToInt32(data["job_source_target_record_id"]);

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

            var flowService = (FlowService)services.GetService(typeof(FlowService));

            job.Job_State = SosyncState.New;
            job.Job_Fetched = DateTime.UtcNow;
            job.Job_To_FSO_Can_Sync = false;

            if (flowService.ModelPriorities.ContainsKey(job.Job_Source_Model))
                job.Job_Priority = flowService.ModelPriorities[job.Job_Source_Model];
            else
                job.Job_Priority = ModelPriority.Default;

            return job;
        }

        private void StoreJob(IServiceProvider services, SyncJob job)
        {
            using (var db = (DataService)services.GetService(typeof(DataService)))
            {
                db.CreateJob(job, "");
            }
        }
        
        [HttpGet("retryfailed")]
        public IActionResult RetryFailed([FromServices] DataService db)
        {
            db.ReopenErrorJobs();
            _jobWorker.Start();

            return new OkResult();
        }
        #endregion
    }
}
