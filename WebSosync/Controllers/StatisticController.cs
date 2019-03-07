using Microsoft.AspNetCore.Mvc;
using Syncer.Attributes;
using Syncer.Flows;
using Syncer.Services;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebSosync.Models;
using WebSosync.Services;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class StatisticController : Controller
    {
        private StatisticService _stat;
        private FlowService _flows;

        public StatisticController(StatisticService stat, FlowService flows)
        {
            _stat = stat;
            _flows = flows;
        }

        [HttpGet("studio/queue")]
        public async Task<IActionResult> StudioQueue()
        {
            var result = await _stat.GetMssqlQueueStatisticAsync();
            return new OkObjectResult(result);
        }

        [HttpGet("studio/flows")]
        public async Task<IActionResult> StudioFlows()
        {
            var flowNames = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f => f.GetCustomAttribute<StudioModelAttribute>().Name);

            var result = new FlowStatistic() {
                UnsynchronizedModels = await _stat.GetMssqlModelStatisticsAsync(flowNames)
            };

            return new OkObjectResult(result);
        }
    }
}