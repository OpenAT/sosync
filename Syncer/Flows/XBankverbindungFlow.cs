using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBankverbindung")]
    [OnlineModel(Name = "frst.xbankverbindung")]
    public class XBankverbindungFlow
        : ReplicateSyncFlow
    {
        public XBankverbindungFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxBankverbindung>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<dboxBankverbindung, frstxBankverbindung>(
                studioID,
                action,
                studio => studio.xBankverbindungID,
                (studio, online) =>
                {
                    online.Add("beschreibung", studio.Beschreibung);
                    online.Add("kurzbezeichnung", studio.Kurzbezeichnung);
                    online.Add("bankleitzahl", studio.Bankleitzahl);
                    online.Add("kontonummer", studio.Kontonummer);
                    online.Add("xiban", studio.xIBAN);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Kein sync zu FS
            throw new NotSupportedException();
        }
    }
}
