using System;
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

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonEmail")]
    [OnlineModel(Name = "frst.personemail")]
    public class PersonEmailDeleteFlow
        : DeleteSyncFlow
    {
        public PersonEmailDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstPersonemail>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboPersonEmail>(onlineID);
        }
    }
}
