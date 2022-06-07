using Microsoft.AspNetCore.Mvc;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Flows;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WebSosync.Models;

namespace WebSosync.Controllers
{
    [Route("[controller]")]
    public class ModelsController : Controller
    {
        private FlowService _flows;

        public ModelsController(FlowService flowService)
        {
            _flows = flowService;
        }

        public IActionResult Index()
        {
            var studioModels = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f => new SyncModelDetailsDto
                {
                    Model = f.GetCustomAttribute<StudioModelAttribute>().Name,
                    ConcurrencyWinner = f.GetCustomAttribute<ConcurrencyOnlineWinsAttribute>() is null
                        ? "studio" : "online",
                    Priority = _flows.ModelPriorities.ContainsKey(f.GetCustomAttribute<StudioModelAttribute>().Name)
                        ? _flows.ModelPriorities[f.GetCustomAttribute<StudioModelAttribute>().Name]
                        : 1000,
                });

            var onlineModels = _flows
                .GetFlowTypes<ReplicateSyncFlow>()
                .Select(f => new SyncModelDetailsDto
                {
                    Model = f.GetCustomAttribute<OnlineModelAttribute>().Name,
                    ConcurrencyWinner = f.GetCustomAttribute<ConcurrencyOnlineWinsAttribute>() is null
                        ? "studio" : "online",
                    Priority = _flows.ModelPriorities.ContainsKey(f.GetCustomAttribute<StudioModelAttribute>().Name)
                        ? _flows.ModelPriorities[f.GetCustomAttribute<StudioModelAttribute>().Name]
                        : 1000,
                });

            return new OkObjectResult(new SyncModelsDto()
            {
                Studio = studioModels.ToArray(),
                Online = onlineModels.ToArray()
            });
        }
    }
}
