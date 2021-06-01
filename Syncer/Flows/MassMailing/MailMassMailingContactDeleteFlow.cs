using DaDi.Odoo.Models.MassMailing;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_contact")]
    [OnlineModel(Name = "mail.mass_mailing.contact")]
    public class MailMassMailingContactDeleteFlow
        : DeleteSyncFlow
    {
        public MailMassMailingContactDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<mailMassMailingContact>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsonmail_mass_mailing_contact>(onlineID);
        }
    }
}
