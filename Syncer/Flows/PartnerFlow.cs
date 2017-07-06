﻿using Syncer.Attributes;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dboPerson")]
    [OnlineModel(Name = "res.Partner")]
    public class PartnerFlow : SyncFlow
    {
        #region Constructors
        public PartnerFlow(IServiceProvider svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            // return the write with odoo client
            return null;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Read write date of
            // - Person
            // - PersonAdresse
            // - PersonEmail
            // - ...

            // Return the most recent write date
            return null;
        }

        protected override void ConfigureOnlineToStudio(SyncJob sourceJob)
        {
            // Before a partner can be synced to studio, the
            // res.company must be synced first
            RequireModel(SosyncSystem.FSOnline, "res.company", 0);
        }

        protected override void ConfigureStudioToOnline(SyncJob sourceJob)
        {
            // Before a partner can be synced to online, the
            // dboxBPKAccount must be synced first
            RequireModel(SosyncSystem.FundraisingStudio, "dboxBPKAccount", 0);
        }
        #endregion
    }
}
