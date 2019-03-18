using DaDi.Odoo.Models;
using Microsoft.AspNetCore.Mvc;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebSosync.Interfaces;
using WebSosync.Models;

namespace WebSosync.Controllers.Statistic
{
    [Route("statistic/online")]
    public class OnlineStatisticController : Controller, IStatisticsController
    {
        private FlowService _flows;
        private OdooService _odoo;

        public OnlineStatisticController(FlowService flowService, OdooService odoo)
        {
            _flows = flowService;
            _odoo = odoo;
        }

        [HttpGet("queue")]
        public async Task<IActionResult> Queue()
        {
            var result = await Task.Run(() =>
            {
                var stat = new QueueStatistic();

                var searchArgs = new List<OdooSearchArgument>();
                stat.TotalJobs = _odoo.Client.SearchCount("sosync.job.queue", searchArgs);

                searchArgs.Add(new OdooSearchArgument("submission_state", "=", "submitted"));
                stat.SubmittedJobs = _odoo.Client.SearchCount("sosync.job.queue", searchArgs);

                return stat;
            });

            return new OkObjectResult(result);
        }

        [HttpGet("flows")]
        public async Task<IActionResult> Flows()
        {
            var flowNames = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f => f.GetCustomAttribute<OnlineModelAttribute>().Name);

            var result = await Task.Run(() => {
                var stat = new FlowStatistic();
                var searchArgs = new List<OdooSearchArgument>();
                searchArgs.Add(new OdooSearchArgument("sosync_fs_id", "=", false));

                foreach (var flowName in flowNames)
                {
                    stat.UnsynchronizedModels.Add(
                        flowName,
                        _odoo.Client.SearchCount(flowName, searchArgs));
                }

                return stat;
            });

            return new OkObjectResult(result);
        }
    }
}