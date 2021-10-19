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

                var searchArgsFalse = new List<OdooSearchArgument>();
                searchArgsFalse.Add(new OdooSearchArgument("sosync_fs_id", "=", false));

                var searchArgsZero = new List<OdooSearchArgument>();
                searchArgsZero.Add(new OdooSearchArgument("sosync_fs_id", "=", 0));

                var messageSearchArgsFalse = new List<OdooSearchArgument>();
                messageSearchArgsFalse.Add(new OdooSearchArgument("sosync_fs_id", "=", false));
                messageSearchArgsFalse.Add(new OdooSearchArgument("subtype_xml_id", "=ilike", "fso_mail_message_subtypes%"));

                var messageSearchArgsZero = new List<OdooSearchArgument>();
                messageSearchArgsZero.Add(new OdooSearchArgument("sosync_fs_id", "=", 0));
                messageSearchArgsZero.Add(new OdooSearchArgument("subtype_xml_id", "=ilike", "fso_mail_message_subtypes%"));

                var searchArgs = searchArgsFalse;
                var searchArgs2 = searchArgsZero;

                foreach (var flowName in flowNames)
                {
                    try
                    {
                        if (flowName == "mail.message")
                        {
                            // Only specific mail.message sub types are synchronized, so a
                            // different search domain is used to check for unsynchronized
                            searchArgs = messageSearchArgsFalse;
                            searchArgs2 = messageSearchArgsZero;
                        }

                        stat.UnsynchronizedModelsCount.Add(
                            flowName,
                            _odoo.Client.SearchCount(flowName, searchArgs)
                            + _odoo.Client.SearchCount(flowName, searchArgs2));
                    }
                    catch (Exception ex)
                    {
                        var isXmlRpcError = ex.Source == "DaDi.XmlRpc";
                        var isDoesNotExistError = _odoo.Client.LastResponseRaw.Contains($"Object {flowName} doesn't exist");
                        var isInvalidFieldSosyncError =
                            _odoo.Client.LastResponseRaw.Contains($"crm_lead")
                            && _odoo.Client.LastResponseRaw.Contains($"Invalid field 'sosync_fs_id'");

                        // If it's anything but one of those errors, rethrow
                        if (!(isXmlRpcError || isDoesNotExistError || isInvalidFieldSosyncError))
                            throw;
                    }
                }

                return stat;
            });

            return new OkObjectResult(result);
        }
    }
}