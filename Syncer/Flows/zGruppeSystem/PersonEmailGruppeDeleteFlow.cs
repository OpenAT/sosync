﻿using System;
using System.Collections.Generic;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.PersonEmailGruppe")]
    [OnlineModel(Name = "frst.personemailgruppe")]
    public class PersonEmailGruppeDeleteFlow
        : DeleteSyncFlow
    {
        public PersonEmailGruppeDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstPersonemailgruppe>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboPersonEmailGruppe>(onlineID);
        }
    }
}
