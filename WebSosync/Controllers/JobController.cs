using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using System;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class JobController
    {
        #region Members
        private SosyncOptions _config;
        #endregion
        
        #region Constructors
        public JobController(SosyncOptions config)
        {
            _config = config;
        }
        #endregion

        #region Methods
        [HttpGet()]
        public IActionResult Get(SyncJobDto jobDto)
        {
            try
            {
                using (var db = new DataService(_config))
                {
                    // Map the transfer object to a new sync job object
                    var job = Mapper.Map<SyncJobDto, SyncJob>(jobDto);

                    // Defaults
                    job.State = SosyncState.New;
                    job.Fetched = DateTime.Now.ToUniversalTime();

                    db.CreateSyncJob(job);
                }

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
