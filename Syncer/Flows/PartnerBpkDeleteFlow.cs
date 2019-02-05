using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using WebSosync.Data.Models;
using dadi_data.Models;
using System.Linq;
using Syncer.Exceptions;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using WebSosync.Common;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonBPK")]
    [OnlineModel(Name = "res.partner.bpk")]
    public class PartnerBpkDeleteFlow : DeleteSyncFlow
    {
        public PartnerBpkDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"Delete from [fs] to [fso] for model {StudioModelName}.");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboPersonBPK>(onlineID);
        }
    }
}
