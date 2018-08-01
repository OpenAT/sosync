using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeDeteleFlow
        : DeleteSyncFlow
    {
        public zGruppeDeteleFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
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
