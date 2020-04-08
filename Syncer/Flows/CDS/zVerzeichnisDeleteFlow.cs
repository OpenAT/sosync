using DaDi.Odoo.Models.CDS;
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

namespace Syncer.Flows.CDS
{
    [StudioModel(Name = "dbo.zVerzeichnis")]
    [OnlineModel(Name = "frst.zverzeichnis")]
    public class zVerzeichnisDeleteFlow
        : DeleteSyncFlow
    {
        public zVerzeichnisDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstzVerzeichnis>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dbozVerzeichnis>(onlineID);
        }
    }
}
