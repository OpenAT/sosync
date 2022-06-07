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
            Func<SyncTargetStudioAttribute, SyncTargetOnlineAttribute, string> getSyncDirection = (studioAtt, onlineAtt) =>
            {
                if (studioAtt != null && onlineAtt != null)
                {
                    return "both";
                }

                if (studioAtt is null && onlineAtt is null)
                {
                    throw new Exception("Sync target attributes missing completely");
                }

                if (studioAtt is null)
                {
                    return "to-online-only";
                }

                return "to-studio-only";
            };

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
                    SyncDirection = getSyncDirection(
                        f.GetCustomAttribute<SyncTargetStudioAttribute>(),
                        f.GetCustomAttribute<SyncTargetOnlineAttribute>()
                        ),
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
                    SyncDirection = getSyncDirection(
                        f.GetCustomAttribute<SyncTargetStudioAttribute>(),
                        f.GetCustomAttribute<SyncTargetOnlineAttribute>()
                        ),
                });

            return new OkObjectResult(new SyncModelsDto()
            {
                Studio = studioModels.ToArray(),
                Online = onlineModels.ToArray()
            });
        }
    }
}
