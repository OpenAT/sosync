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
using System.Linq;
using WebSosync.Data;
using WebSosync.Data.Constants;
using DaDi.Odoo;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_list")]
    [OnlineModel(Name = "mail.mass_mailing.list")]
    [SyncTargetStudio, SyncTargetOnline]
    public class MailMassMailingListFlow
        : ReplicateSyncFlow
    {
        public MailMassMailingListFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonmail_mass_mailing_list>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "zgruppedetail_id" });
            var zgruppedetail_id = OdooConvert.ToInt32ForeignKey(odooModel["zgruppedetail_id"], allowNull: true);

            if (zgruppedetail_id.HasValue)
            {
                RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppedetail", zgruppedetail_id.Value, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<fsonmail_mass_mailing_list>())
            {
                var studioModel = db.Read(new { mail_mass_mailing_listID = studioID }).SingleOrDefault();

                if (studioModel.zGruppeDetailID.HasValue)
                {
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zGruppeDetail", studioModel.zGruppeDetailID.Value, SosyncJobSourceType.Default);
                }
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsonmail_mass_mailing_list, mailMassMailingList>(
                studioID,
                action,
                x => x.mail_mass_mailing_listID,
                (studio, online) => {
                    int? zgruppedetail_id = null;

                    if (studio.zGruppeDetailID.HasValue)
                    {
                        zgruppedetail_id = GetOnlineID<dbozGruppeDetail>(
                            "dbo.zGruppeDetail",
                            "frst.zgruppedetail",
                            studio.zGruppeDetailID.Value);
                    }

                    online.Add("zgruppedetail_id", zgruppedetail_id);
                    online.Add("name", studio.name);
                    online.Add("list_type", Svc.TypeService.GetTypeValue(studio.ListtypID));
                    online.Add("partner_mandatory", studio.partner_mandatory);
                    online.Add("bestaetigung_typ", Svc.TypeService.GetTypeValue(studio.BestaetigungtypID));
                    online.Add("bestaetigung_erforderlich", studio.BestaetigungErforderlich);
                    online.Add("goal", studio.goal);
                    online.Add("website_published", studio.website_published);
                    online.Add("goal_dynamic", studio.goal_dynamic);
                    online.Add("system_list", studio.system_list);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<mailMassMailingList, fsonmail_mass_mailing_list>(
                onlineID,
                action,
                x => x.mail_mass_mailing_listID,
                (online, studio) => {
                    var zGruppeDetailID = GetStudioIDFromOnlineReference(
                        "dbo.zGruppeDetail",
                        online,
                        x => x.zgruppedetail_id,
                        false);

                    studio.zGruppeDetailID = zGruppeDetailID;
                    studio.name = online.name;
                    studio.ListtypID = Svc.TypeService.GetTypeID("fsonmail_mass_mailing_list_ListtypID", online.list_type);
                    studio.partner_mandatory = online.partner_mandatory;
                    studio.BestaetigungtypID = Svc.TypeService.GetTypeID("fsonmail_mass_mailing_list_BestaetigungtypID", online.bestaetigung_typ);
                    studio.BestaetigungErforderlich = online.bestaetigung_erforderlich;
                    studio.goal = online.goal;
                    studio.fso_create_date = online.create_date;
                    studio.fso_write_date = online.write_date;
                    studio.website_published = online.website_published;
                    studio.goal_dynamic = online.goal_dynamic;
                    studio.system_list = online.system_list;
                });
        }
    }
}
