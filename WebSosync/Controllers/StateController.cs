using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebSosync.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class StateController : Controller
    {
        #region Constructors
        public StateController(IBackgroundJob background, IHostService hostService)
        {
            _job = background;
            _hostService = hostService;
        }
        #endregion

        #region Methods
        // GET state
        [HttpGet]
        public IActionResult Get()
        {
            var result = new StateResult()
            {
                State = (int)_job.Status,
                StateDescription = $"{_job.Status.ToString()}"
            };

            return new OkObjectResult(result);
        }

        // GET state/run
        [HttpGet]
        [Route("start")]
        public IActionResult Start()
        {
            // States + Descriptions
            // 0: AlreadyRunningRestartRequested
            // 1: Started
            // 2: ShutdownInProgress

            StateResult result;
            
            // If a shutdown is pending, terminate the request as bad request
            if (_job.ShutdownPending)
            {
                result = new StateResult()
                {
                    State = 2,
                    StateDescription = "ShutdownInProgress"
                };

                return new BadRequestObjectResult(result);
            }

            // If no shutdown is pending, handle the request normally
            bool startedNew = false;

            // If the job is currently stopped or had an error, attempt to start it
            if (_job.Status == ServiceState.Stopped || _job.Status == ServiceState.Error)
            {
                _job.Start();
                startedNew = true;
            }
            else
            {
                _job.RestartOnFinish = true;
            }

            result = new StateResult()
            {
                State = startedNew ? 1 : 0,
                StateDescription = startedNew ? "Started" : "AlreadyRunningRestartRequested"
            };

            return new OkObjectResult(result);
        }

        // GET state/stop
        [HttpGet]
        [Route("stop")]
        public IActionResult Stop()
        {
            // States + Descriptions
            // 0: StopAlreadyRequested
            // 1: StopRequested

            // If no shutdown is pending, handle the request normally
            bool didStop = false;

            // Attempt to stop the job
            if (_job.Status != ServiceState.Stopped && _job.Status != ServiceState.Stopping && _job.Status != ServiceState.Error)
            {
                _job.Stop();
                didStop = true;
            }

            var result = new StateResult()
            {
                State = didStop ? 1 : 0,
                StateDescription = didStop ? "StopRequested" : "StopAlreadyRequested"
            };

            if (didStop)
                return new OkObjectResult(result);
            else
                return new BadRequestObjectResult(result);
        }

        //// GET api/values/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}
        #endregion

        #region Members
        private IBackgroundJob _job;
        private IHostService _hostService;
        #endregion
    }
}
