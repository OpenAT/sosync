using Microsoft.AspNetCore.Mvc;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Services;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebSosync.Interfaces;
using WebSosync.Models;
using WebSosync.Services;

namespace WebSosync.Controllers
{
    [Route("statistic/studio")]
    public class StudioStatisticController : Controller, IStatisticsController
    {
        private StatisticService _stat;
        private FlowService _flows;

        public StudioStatisticController(StatisticService stat, FlowService flows)
        {
            _stat = stat;
            _flows = flows;
        }

        [HttpGet("queue")]
        public async Task<IActionResult> Queue()
        {
            var result = await _stat.GetMssqlQueueStatisticAsync();
            return new OkObjectResult(result);
        }

        [HttpGet("flows")]
        public async Task<IActionResult> Flows()
        {
            var flowNames = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f => f.GetCustomAttribute<StudioModelAttribute>().Name);

            var result = new FlowStatistic() {
                UnsynchronizedModelsCount = await _stat.GetMssqlModelStatisticsAsync(flowNames)
            };

            return new OkObjectResult(result);
        }
    }
}