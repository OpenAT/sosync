﻿using System;
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
        public zGruppeDetailDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstzGruppedetail>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dbozGruppeDetail>(onlineID);
        }
    }
}
