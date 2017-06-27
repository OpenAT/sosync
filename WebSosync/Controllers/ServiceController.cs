using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebSosync.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WebSosync.Services;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class ServiceController : Controller
    {
        #region Members
        private IBackgroundJob _job;
        private IHostService _hostService;
        private ILogger<ServiceController> _log;
        #endregion

        #region Constructors
        public ServiceController(IBackgroundJob background, IHostService hostService, ILogger<ServiceController> logger)
        {
            _job = background;
            _hostService = hostService;
            _log = logger;
        }
        #endregion

        #region Methods
        // GET service/status
        [HttpGet("status")]
        public IActionResult Get()
        {
            var result = new StateResult()
            {
                State = (int)_job.Status,
                StateDescription = $"{_job.Status.ToString()}"
            };

            return new OkObjectResult(result);
        }

        //// GET service/start
        //[HttpGet]
        //[Route("start")]
        //public IActionResult Start()
        //{
        //    try
        //    {
        //        _job.Start();
        //        return new OkResult();
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.LogError(ex.ToString());
        //        return new BadRequestResult();
        //    }
        //}

        // service/version
        [HttpGet("version")]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult Version()
        {
            var result = "";

            try
            {
                var git = new Git();
                result = git.GetCommitId();
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _log.LogError(ex.ToString());
                return new BadRequestObjectResult("Could not read version.");
            }
        }

        //// GET api/values/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}
        #endregion
    }
}
