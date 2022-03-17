using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PostgreSQLCopyHelper;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WebSosync.Common;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;
using WebSosync.Enumerations;
using WebSosync.Extensions;
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
        private SosyncGuiContext _ctx;
        private FlowService _flowService;
        #endregion

        #region Constructors
        public JobController(
            IBackgroundJob<SyncWorker> worker,
            ILogger<JobController> logger,
            SosyncGuiContext context,
            FlowService flowService)
        {
            _jobWorker = worker;
            _log = logger;
            _ctx = context;
            _flowService = flowService;
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

        [HttpPost("create")]
        public IActionResult Post([FromServices]IServiceProvider services, [FromBody]JsonObject data)
        {
            if (data == null)
                return new BadRequestResult();

            var result = ValidateDictionary(data);

            if (result.ErrorCode != (int)JobErrorCode.None)
                return new BadRequestObjectResult(result);

            var job = ParseJob(services, data);
            // create_date wurde bisher bei dieser route nicht gesetz, jobs konnten nicht mehr verarbeitet werden, weil nciht mehr in dem new jobs select:
            job.Create_Date = DateTime.UtcNow;
            StoreJob(services, job);
            result.ID = job.ID;

            // Start the sync background job
            _jobWorker.Start();

            return new OkObjectResult(result);
        }

        [HttpPost("bulk-create")]
        [RequestSizeLimit(1073741824000)]
        public IActionResult Post([FromServices]IServiceProvider services, [FromBody]JsonObject[] dictionaryList)
        {
            if (dictionaryList == null || dictionaryList.Length == 0)
            {
                var errorMsg = string.Join(
                    "\n",
                    ModelState.Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));

                return new BadRequestObjectResult(errorMsg);
            }

            var isBadRequest = false;

            StringBuilder errorLog = new StringBuilder(1024 * 2);

            // Validate all dictionaries in batch
            foreach (var dictionary in dictionaryList)
            {
                var validationResult = ValidateDictionary(dictionary);

                if (validationResult.ErrorCode != (int)JobErrorCode.None)
                {
                    if (dictionary.ContainsKey("job_source_record_id") && Convert.ToString(dictionary["job_source_record_id"]) == "0")
                    {
                        errorLog.AppendLine("job_source_record_id = 0 is invalid");
                    }

                    isBadRequest = true;
                }
            }

            // If no validation failed, create all jobs
            if (!isBadRequest)
            {
                var jobs = new List<SyncJob>(dictionaryList.Length);

                for (int i = 0; i < dictionaryList.Length; i++)
                {
                    jobs.Add(ParseJob(services, dictionaryList[i]));
                    //var job = ParseJob(services, dictionaryList[i]);
                }

                StoreJobs(services, jobs);

                // Start the sync background job
                _jobWorker.Start();

                return new OkObjectResult("success");
            }

            var errorMessage = "error\n" + errorLog.ToString();
            _log.LogInformation(errorMessage);
            return new BadRequestObjectResult(errorMessage);
        }

        private void SetJobEntityDefaults(IEnumerable<SosyncJobEntity> jobs)
        {
            foreach (var job in jobs)
            {
                job.JobState = SosyncState.New.Value;
                job.JobFetched = DateTime.UtcNow;

                if (job.JobPriority == null)
                {
                    job.JobPriority = _flowService.GetModelPriority(job.JobSourceModel);
                }
            }
        }

        [HttpPost("tree-create")]
        [RequestSizeLimit(1073741824000)]
        public async Task<IActionResult> Post([FromBody]JobDto[] jobs)
        {
            if (ModelState.IsValid)
            {
                var jobEntities = jobs
                    .SelectMany(j => j.ToEntities())
                    .ToList();

                SetJobEntityDefaults(jobEntities);

                _ctx.AddRange(jobEntities);
                await _ctx.SaveChangesAsync();

                _jobWorker.Start();

                return new OkResult();
            }
            else
            {
                var s = ModelState.Select(x => x.Key + ": " + string.Join(" ", x.Value.Errors.Select(err => err.ErrorMessage)));
                return new BadRequestObjectResult(s);
            }
        }

        private JobResultDto ValidateDictionary(JsonObject data)
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

        private SyncJob ParseJob(IServiceProvider services, JsonObject data)
        {
            var job = new SyncJob()
            {
                Job_Date = DateTime.Parse(
                    data["job_date"].ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                Job_Source_System = GetNodeValue<string>(data["job_source_system"]),
                Job_Source_Model = GetNodeValue<string>(data["job_source_model"]),
                Job_Source_Record_ID = GetNodeValue<Int32>(data["job_source_record_id"]),
                Write_Date = DateTime.UtcNow,
                Create_Date = DateTime.UtcNow
            };

            if (data.ContainsKey("job_source_type"))
            {
                job.Job_Source_Type = GetNodeValue<string>(data["job_source_type"]);
                
                if (job.Job_Source_Type == "")
                    job.Job_Source_Type = null;


                if (job.Job_Source_Type == SosyncJobSourceType.MergeInto.Value)
                {
                    job.Job_Source_Merge_Into_Record_ID = GetNodeValue<Int32>(data["job_source_merge_into_record_id"]);

                    if (data.ContainsKey("job_source_target_merge_into_record_id"))
                    {
                        job.Job_Source_Target_Merge_Into_Record_ID = GetNodeValue<Int32>(data["job_source_target_merge_into_record_id"]);
                    }
                }
            }

            if (data.ContainsKey("job_source_target_record_id"))
                job.Job_Source_Target_Record_ID = GetNodeValue<Int32>(data["job_source_target_record_id"]);

            if (data.ContainsKey("job_source_sosync_write_date"))
            {
                var val = data["job_source_sosync_write_date"].ToString();

                if (!string.IsNullOrWhiteSpace(val))
                {
                    try
                    {
                        job.Job_Source_Sosync_Write_Date = DateTime.Parse(
                            val,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    }
                    catch (Exception)
                    {
                        throw new Exception($"Unrecognized format ({val}) in field job_source_sosync_write_date.");
                    }
                }
            }

            job.Job_Source_Fields = ParseJobSourceFields(data);

            job.Job_State = SosyncState.New.Value;
            job.Job_Fetched = DateTime.UtcNow;

            job.Job_Priority = ParseJobPriority(services, job, data);

            return job;
        }

        private TValue GetNodeValue<TValue>(JsonNode node)
        {
            if (node.AsValue().TryGetValue<TValue>(out var value))
            {
                return value;
            }
            else if (node.AsValue().TryGetValue<bool?>(out var boolValue) && boolValue == false)
            {
                return default;
            }

            throw new Exception("Content of job_source_fields was not recognized.");
        }

        private string ParseJobSourceFields(JsonObject data)
        {
            if (data.ContainsKey("job_source_fields"))
            {
                return data["job_source_fields"].ToString();
            }
            return null;
        }

        private int ParseJobPriority(IServiceProvider services, SyncJob job, JsonObject data)
        {
            var flowService = (FlowService)services.GetService(typeof(FlowService));

            if (data.ContainsKey("job_priority") && data["job_priority"] != null)
                return GetNodeValue<Int32>(data["job_priority"]);
            else
                return flowService.GetModelPriority(job.Job_Source_Model);
        }

        private void StoreJob(IServiceProvider services, SyncJob job)
        {
            using (var db = (DataService)services.GetService(typeof(DataService)))
            {
                db.CreateJob(job);
            }
        }

        private void StoreJobs(IServiceProvider services, List<SyncJob> jobs)
        {
            var s = Stopwatch.StartNew();
            var create_date = DateTime.UtcNow;

            // Only map relevant fields
            var bulk = new PostgreSQLCopyHelper<SyncJob>("public", "sosync_job")
                .MapTimeStamp("job_date", x => x.Job_Date)
                .MapText("job_state", x => x.Job_State)
                .MapText("job_source_type", x => x.Job_Source_Type)
                .MapText("job_source_system", x => x.Job_Source_System)
                .MapText("job_source_model", x => x.Job_Source_Model)
                .MapInteger("job_source_record_id", x => x.Job_Source_Record_ID)
                .MapInteger("job_source_merge_into_record_id", x => x.Job_Source_Merge_Into_Record_ID)
                .MapInteger("job_source_target_merge_into_record_id", x => x.Job_Source_Target_Merge_Into_Record_ID)
                .MapInteger("job_source_target_record_id", x => x.Job_Source_Target_Record_ID)
                .MapText("job_source_fields", x => x.Job_Source_Fields)
                .MapTimeStamp("job_source_sosync_write_date", x => x.Job_Source_Sosync_Write_Date)
                .MapTimeStamp("write_date", x => x.Write_Date)
                .MapTimeStamp("job_fetched", x => create_date)
                .MapTimeStamp("create_date", x => create_date)
                .MapInteger("job_priority", x => x.Job_Priority);

            using (var db = (DataService)services.GetService(typeof(DataService)))
            {
                var trans = db.BeginTransaction();
                bulk.SaveAll(db.Connection, jobs);
                trans.Commit();
            }

            s.Stop();
            _log.LogInformation($"Bulk-Insert of {jobs.Count} jobs took {SpecialFormat.FromMilliseconds(s.Elapsed.Milliseconds)}");
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
