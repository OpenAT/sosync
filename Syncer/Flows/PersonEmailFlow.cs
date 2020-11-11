using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonEmail")]
    [OnlineModel(Name = "frst.personemail")]
    [ModelPriority(4500)]
    public class PersonEmailFlow
        : ReplicateSyncFlow
    {
        public PersonEmailFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonEmail>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<dboPersonEmail>())
            {
                var studioModel = db.Read(new { PersonEmailID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", odooPartnerID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var partner_id = 0;

            using (var db = Svc.MdbService.GetDataService<dboPersonEmail>())
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
                    // state -- is only set by FSOnline
                    // main_address -- is only set by FSOnline

                    online.Add("bestaetigt_typ", (object)Svc.TypeService
                        .GetTypeValue(studio.BestaetigungsTypID) ?? false);

                    online.Add("bestaetigt_am_um", DateTimeHelper.ToUtc(studio.BestaetigtAmUm));
                    online.Add("bestaetigt_herkunft", studio.BestaetigungsHerkunft);
                    online.Add("anrede_lang", studio.AnredeLang);
                    online.Add("forced_main_address", studio.HauptAdresseErzwingen);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = Svc.OdooService.Client.GetDictionary(
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
                    EmailHelper.SplitEmail(online.email, out var mailVor, out var mailNach);

                    studio.PersonID = PersonID;
                    studio.EmailVor = mailVor;
                    studio.EmailNach = mailNach;
                    studio.GültigVon = online.gueltig_von.Date;
                    studio.GültigBis = online.gueltig_bis.Date;
                    studio.HauptAdresse = online.main_address;
                    studio.State = online.state;

                    studio.BestaetigungsTypID = Svc.TypeService
                        .GetTypeID("PersonEmail_BestaetigungsTypID", online.bestaetigt_typ);

                    studio.BestaetigtAmUm = DateTimeHelper.ToLocal(online.bestaetigt_am_um);
                    studio.BestaetigungsHerkunft = online.bestaetigt_herkunft;
                    // studio.AnredeLang // Do not set
                    studio.HauptAdresseErzwingen = online.forced_main_address;
                });
        }
    }
}
