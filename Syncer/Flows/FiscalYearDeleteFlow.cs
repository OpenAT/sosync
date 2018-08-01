using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using WebSosync.Data.Models;
using dadi_data.Models;
using System.Linq;
using Syncer.Exceptions;
using DaDi.Odoo.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKMeldespanne")]
    [OnlineModel(Name = "account.fiscalyear")]
    public class FiscalYearDeleteFlow : DeleteSyncFlow
    {
        public FiscalYearDeleteFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<accountFiscalYear>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboxBPKMeldespanne>(onlineID);
        }
    }
}
