using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace WebSosync.Controllers
{
    // job
    [Route("[controller]")]
    public class JobController
    {
        #region Members
        private DataService _db;
        #endregion
        
        #region Constructors
        public JobController(DataService db)
        {
            _db = db;
        }
        #endregion

        #region Methods
        // job/create
        [HttpGet("create")]
        public IActionResult Get(SyncJobDto jobDto)
        {
            try
            {
                // Map the transfer object to a new sync job object
                var job = Mapper.Map<SyncJobDto, SyncJob>(jobDto);

                // Defaults
                job.State = SosyncState.New;
                job.Fetched = DateTime.Now.ToUniversalTime();

                _db.CreateJob(job);

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
