using DaDi.Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBankverbindung")]
    [OnlineModel(Name = "frst.xbankverbindung")]
    public class XBankverbindungDeleteFlow
        : DeleteSyncFlow
    {
        public XBankverbindungDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<frstxBankverbindung>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Kein sync zu FS
            throw new NotSupportedException("");
        }
    }
}
