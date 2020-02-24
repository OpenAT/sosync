using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using WebSosync.Data.Models;
using WebSosync.Common;
using DaDi.Odoo;
using WebSosync.Data;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.crm_lead")]
    [OnlineModel(Name = "crm.lead")]
    public class CrmLeadFlow
        : ReplicateSyncFlow
    {
        public CrmLeadFlow(
            ILogger logger,
            OdooService odooService,
            SosyncOptions conf,
            FlowService flowService,
            OdooFormatService odooFormatService,
            SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsoncrm_lead>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var lead = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "company_id", "partner_id" });
            var companyID = OdooConvert.ToInt32((string)((List<object>)lead["company_id"])[0]);
            var partnerID = OdooConvert.ToInt32((string)((List<object>)lead["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"Model {StudioModelName}/{OnlineModelName} can only be synchronized from {SosyncSystem.FSOnline} to {SosyncSystem.FundraisingStudio}.");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<crmLead, fsoncrm_lead>(
                onlineID,
                action,
                x => x.crm_leadID,
                (online, studio) => {
                    var xBPKAccountID = GetStudioIDFromOnlineReference(
                        "dbo.xBPKAccount",
                        online,
                        x => x.company_id,
                        true);

                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.partner_id,
                        true);

                    studio.xBPKAccountID = xBPKAccountID;
                    studio.PersonID = personID;

                    studio.partner_name = online.partner_name;

                    studio.name = online.name;
                    studio.contact_name = online.contact_name;
                    studio.contact_lastname = online.contact_lastname;
                    studio.contact_anrede_individuell = online.contact_anrede_individuell;
                    studio.contact_birthdate_web = online.contact_birthdate_web;
                    studio.contact_newsletter_web = online.contact_newsletter_web;
                    studio.contact_title_web = online.contact_title_web;
                    studio.title = (string)online.title?[1];
                    studio.title_action = online.title_action;
                    studio.function = online.function;

                    studio.email_from = online.email_from;
                    studio.phone = online.phone;
                    studio.mobile = online.mobile;
                    studio.fax = online.fax;

# warning TODO: Implement LandID in sync and set it appropriately
                    studio.LandID = null;

                    studio.state_id = OdooConvert.ToInt32ForeignKey(online.state_id, true);
                    studio.zip = online.zip;
                    studio.city = online.city;
                    studio.street = online.street;
                    studio.street2 = online.street2;
                    studio.contact_street_number_web = online.contact_street_number_web;

                    studio.description = online.description;

                    studio.date_action = online.date_action;
                    studio.date_action_last = online.date_action_last;
                    studio.date_action_next = online.date_action_next;
                    studio.date_assign = online.date_assign;
                    studio.date_closed = online.date_closed;
                    studio.date_deadline = online.date_deadline;
                    studio.date_last_stage_update = online.date_last_stage_update;
                    studio.date_open = online.date_open;
                    studio.day_close = (decimal?)online.day_close;
                    studio.day_open = (decimal?)online.day_open;
                    studio.opt_out = online.opt_out;

                    studio.partner_address_email = online.partner_address_email;
                    studio.partner_address_name = online.partner_address_name;
                    studio.payment_mode = (string)online.payment_mode?[1];

                    studio.type = online.type;

                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
