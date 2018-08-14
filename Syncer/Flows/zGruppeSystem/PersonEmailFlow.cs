﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.PersonEmail")]
    [OnlineModel(Name = "frst.personemail")]
    public class PersonEmailFlow
        : ReplicateSyncFlow
    {
        public PersonEmailFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonEmail>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonEmail>())
            {
                var studioModel = db.Read(new { PersonEmailID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", odooPartnerID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var partner_id = 0;

            using (var db = MdbService.GetDataService<dboPersonEmail>())
            {
                // Get the referenced Studio-IDs
                var personEmail = db.Read(new { PersonEmailID = studioID }).SingleOrDefault();

                partner_id = GetOnlineID<dboPerson>(
                    "dbo.Person",
                    "res.partner",
                    personEmail.PersonID)
                    .Value;
            }

            SimpleTransformToOnline<dboPersonEmail, frstPersonemail>(
                studioID,
                action,
                studioModel => studioModel.PersonEmailID,
                (studio, online) =>
                    {
                        online.Add("email", EmailHelper.MergeEmail(studio.EmailVor, studio.EmailNach));
                        // last_email_update // How to deal with this on MSSQL?
                        online.Add("partner_id", partner_id);
                        online.Add("gueltig_von", studio.GültigVon.Date);
                        online.Add("gueltig_bis", studio.GültigBis.Date);
#warning TODO: Sync state and main_address to PersonEmail
                        // state // Sync this after MSSQL has the column!
                        // main_address // Sync this after MSSQL has the column!
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0])
                .Value;

            // Get the corresponding Studio-IDs
            var PersonID = GetStudioID<dboPersonEmail>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            SimpleTransformToStudio<frstPersonemail, dboPersonEmail>(
                onlineID,
                action,
                studioModel => studioModel.PersonEmailID,
                (online, studio) =>
                    {
                        EmailHelper.SplitEmail(online.email, out var vor, out var nach);

                        studio.PersonID = PersonID;
                        studio.EmailVor = vor;
                        studio.EmailNach = nach;
                        studio.GültigVon = online.gueltig_von.Date;
                        studio.GültigBis = online.gueltig_bis.Date;
                    });
        }
    }
}
