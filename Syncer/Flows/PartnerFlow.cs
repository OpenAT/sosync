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
            // Get and return the foreign id and write date for the res.partner
            return null;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Read the foreign id from dboPerson

            // Read write date of
            // - dboPerson
            // - dboPersonAdresse
            // - dboPersonEmail
            // - ...

            // Return the foreign id and the most recent write date
            return null;
        }

        protected override void ConfigureOnlineToStudio(int onlineID)
        {
            // Since this is a partner flow, onlineID represents the
            // res.partner id.

            // Use that partner id to get the company id
            var companyID = 0;

            // Tell the sync flow base, that we require that specific
            // company
            RequireModel(SosyncSystem.FSOnline, "res.company", companyID);
        }

        protected override void ConfigureStudioToOnline(int studioID)
        {
            // Since this is a partner flow, onlineID represents the
            // dboPerson id.

            // Use the person id to get the xBPKAccount id
            var bpkAccount = 0;

            RequireModel(SosyncSystem.FundraisingStudio, "dboxBPKAccount", bpkAccount);
        }

        protected override void TransformToOnline(int studioID)
        {
            // Load studio model, save it to online
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID)
        {
            // Load online model, save it to studio
            throw new NotImplementedException();
        }
        #endregion
    }
}
