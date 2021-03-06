﻿using Microsoft.AspNetCore.Mvc;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Linq;
using System.Reflection;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class FlowController : Controller
    {
        private FlowService _flows;
        public FlowController(FlowService flowService)
        {
            _flows = flowService;
        }

        [HttpGet("map")]
        public IActionResult Map()
        {
            var result = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f =>
                {
                    return new Tuple<string, string>(
                        f.GetCustomAttribute<StudioModelAttribute>().Name,
                        f.GetCustomAttribute<OnlineModelAttribute>().Name);
                })
                .ToDictionary(tup => tup.Item1, tup => tup.Item2);

            return new OkObjectResult(result);
        }
    }
}