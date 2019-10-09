using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;
using WebSosync.Data.Models;
using dadi_data.Models;
using DaDi.Odoo.Models.MassMailing;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_list")]
    [OnlineModel(Name = "mail.mass_mailing.list")]
    public class MailMassMailingListFlow
        : ReplicateSyncFlow
    {
        public MailMassMailingListFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService) : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonmail_mass_mailing_list>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsonmail_mass_mailing_list, mailMassMailingList>(
                studioID,
                action,
                x => x.mail_mass_mailing_listID,
                (studio, online) => {
                    online.Add("name", studio.name);
                    online.Add("list_type", MdbService.GetTypeValue(studio.ListtypID));
                    online.Add("partner_mandatory", studio.partner_mandatory);
                    online.Add("bestaetigung_typ", MdbService.GetTypeValue(studio.BestaetigungtypID));
                    online.Add("bestaetigung_erforderlich", studio.BestaetigungErforderlich);
                    online.Add("goal", studio.goal);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<mailMassMailingList, fsonmail_mass_mailing_list>(
                onlineID,
                action,
                x => x.mail_mass_mailing_listID,
                (online, studio) => {
                    studio.name = online.name;
                    studio.ListtypID = MdbService.GetTypeID("fsonmail_mass_mailing_list_ListtypID", online.list_type);
                    studio.partner_mandatory = online.partner_mandatory;
                    studio.BestaetigungtypID = MdbService.GetTypeID("fsonmail_mass_mailing_list_BestaetigungtypID", online.bestaetigung_typ);
                    studio.BestaetigungErforderlich = online.bestaetigung_erforderlich;
                    studio.goal = online.goal;
                    studio.fso_create_date = online.create_date;
                    studio.fso_write_date = online.write_date;
                });
        }
    }
}
