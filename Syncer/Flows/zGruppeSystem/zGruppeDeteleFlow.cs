using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Services;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeDeteleFlow
        : DeleteSyncFlow
    {
        public zGruppeDeteleFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService)
            : base(logger, odooService, conf, flowService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstzGruppe>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new SyncerException($"Model {StudioModelName} can only be deleted from FS, not from FS-Online.");
        }
    }
}
