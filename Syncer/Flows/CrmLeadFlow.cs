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
using WebSosync.Data.Constants;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.crm_lead")]
    [OnlineModel(Name = "crm.lead")]
    [ModelPriority(5000)]
    public class CrmLeadFlow
        : ReplicateSyncFlow
    {
        public CrmLeadFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsoncrm_lead>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var lead = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[]
                { 
                    "company_id",
                    "partner_id",
                    "personemailgruppe_id",
                    "frst_zverzeichnis_id"
                });
            
            var companyID = OdooConvert.ToInt32ForeignKey(lead["company_id"], allowNull: false);
            var partnerID = OdooConvert.ToInt32ForeignKey(lead["partner_id"], allowNull: true);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value, SosyncJobSourceType.Default);

            if (partnerID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value, SosyncJobSourceType.Default);

            var emailGroupID = OdooConvert.ToInt32ForeignKey(lead["personemailgruppe_id"], allowNull: true);
            if (emailGroupID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, "frst.personemailgruppe", emailGroupID.Value, SosyncJobSourceType.Default);

            var verzeichnisID = OdooConvert.ToInt32ForeignKey(lead["frst_zverzeichnis_id"], allowNull: true);
            if (verzeichnisID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, "frst.zverzeichnis", verzeichnisID.Value, SosyncJobSourceType.Default);
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
                    studio.xBPKAccountID = xBPKAccountID;

                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.partner_id,
                        true);
                    studio.PersonID = personID;

                    var personEmailGruppeID = GetStudioIDFromOnlineReference(
                        "dbo.PersonEmailGruppe",
                        online,
                        x => x.personemailgruppe_id,
                        false);
                    studio.PersonEmailGruppeID = personEmailGruppeID;

                    var zVerzeichnisID = GetStudioIDFromOnlineReference(
                        "dbo.zVerzeichnis",
                        online,
                        x => x.frst_zverzeichnis_id,
                        false);
                    studio.zVerzeichnisID = zVerzeichnisID;

                    studio.fb_lead_id = online.fb_lead_id;

                    studio.crm_form_id = OdooConvert.ToInt32ForeignKey(online.crm_form_id, true);
                    studio.crm_form_name = OdooConvert.ToForeignKeyName(online.crm_form_id, true);

                    studio.crm_page_id = OdooConvert.ToInt32ForeignKey(online.crm_page_id, true);
                    studio.crm_page_name = OdooConvert.ToForeignKeyName(online.crm_page_id, true);

                    studio.partner_name = online.partner_name;
                    studio.name = online.name;
                    studio.contact_name = online.contact_name;
                    studio.contact_lastname = online.contact_lastname;
                    studio.contact_anrede_individuell = online.contact_anrede_individuell;
                    studio.contact_birthdate_web = online.contact_birthdate_web;
                    studio.contact_newsletter_web = online.contact_newsletter_web;
                    studio.contact_title_web = online.contact_title_web;
                    studio.contact_gender = online.contact_gender;
                    studio.title = (string)online.title?[1];
                    studio.title_action = online.title_action;
                    studio.function = online.function;

                    studio.email_from = online.email_from;
                    studio.phone = online.phone;
                    studio.mobile = online.mobile;
                    studio.fax = online.fax;

                    var countryID = OdooConvert.ToInt32ForeignKey(online.country_id, true);
                    studio.LandID = GetLandIdForCountryId(countryID);

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
                    studio.frst_import_type = online.frst_import_type;

                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
