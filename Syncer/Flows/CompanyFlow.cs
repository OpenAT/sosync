﻿using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Models;
using WebSosync.Data.Models;
using System.Linq;
using Syncer.Enumerations;
using dadi_data.Models;
using Syncer.Exceptions;
using WebSosync.Data;
using Microsoft.Extensions.Logging;
using DaDi.Odoo.Models;
using Syncer.Services;
using WebSosync.Common;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKAccount")]
    [OnlineModel(Name = "res.company")]
    [SyncTargetStudio, SyncTargetOnline]
    public class CompanyFlow : ReplicateSyncFlow
    {
        #region Constructors
        public CompanyFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxBPKAccount>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<dboxBPKAccount, resCompany>(
                studioID,
                action,
                studioModel => studioModel.xBPKAccountID,
                (studio, online) =>
                    {
                        online.Add("name", studio.Name);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<resCompany, dboxBPKAccount>(
                onlineID,
                action,
                studioModel => studioModel.xBPKAccountID,
                (online, studio) =>
                    {
                        studio.Name = online.Name;
                    });
        }
        #endregion
    }
}
