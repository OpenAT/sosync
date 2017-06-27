using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Interfaces;
using WebSosync.Models;

namespace WebSosync.Controllers
{
    // job
    [Route("[controller]")]
    public class JobController
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
        public IActionResult Get(SyncJobDto jobDto)
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
                _job.Start();

                // Just return empty OK result
                return new OkResult();
            }
            catch (Exception ex)
            {
                // Return error without stack trace
                return new BadRequestObjectResult(ex.Message);
            }
        }
        #endregion
    }
}
