using DaDi.Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.AktionSpendenmeldungBPK")]
    [OnlineModel(Name = "res.partner.donation_report")]
    public class PartnerDonationReportDeleteFlow
        : DeleteSyncFlow
    {
        public PartnerDonationReportDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<resPartnerDonationReport>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException("Donation report can only be deleted from FS to FSO");
        }
    }
}
