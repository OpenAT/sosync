using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.crm_lead")]
    [OnlineModel(Name = "crm.lead")]
    public class CrmLeadDeleteFlow
        : DeleteSyncFlow
    {
        public CrmLeadDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsoncrm_lead>(onlineID);
        }
    }
}
