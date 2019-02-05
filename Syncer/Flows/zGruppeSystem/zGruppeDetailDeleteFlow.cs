using System;
using System.Collections.Generic;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "frst.zgruppedetail")]
    public class zGruppeDetailDeleteFlow
        : DeleteSyncFlow
    {
        public zGruppeDetailDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstzGruppedetail>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new SyncerException($"{StudioModelName} can only be deleted from FS, not from FS-Online.");
        }
    }
}
