using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.AktionOnlineToken")]
    [OnlineModel(Name = "res.partner.fstoken")]
    public class TokenFlow
        : ReplicateSyncFlow
    {
        public TokenFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboAktionOnlineToken>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboAktion>())
            {
                var studioModel = db.Read(new { AktionsID = studioID }).SingleOrDefault();

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

            using (var dbAkt = MdbService.GetDataService<dboAktion>())
            {
                // Get the referenced Studio-IDs
                var studioAktion = dbAkt.Read(new { AktionsID = studioID }).SingleOrDefault();

                partner_id = GetOnlineID<dboPerson>(
                    "dbo.Person",
                    "res.partner",
                    studioAktion.PersonID)
                    .Value;
            }

            SimpleTransformToOnline<dboAktionOnlineToken, resPartnerFstoken>(
                studioID,
                action,
                studioModel => studioModel.AktionsID,
                (studio, online) =>
                {
                    online.Add("name", studio.Name);
                    online.Add("partner_id", partner_id);
                    online.Add("expiration_date", studio.Ablaufdatum);
                    online.Add("fs_origin", studio.FsOrigin);
                    online.Add("last_datetime_of_use", studio.LetzteBenutzungAmUm);
                    online.Add("first_datetime_of_use", studio.ErsteBenutzungAmUm);
                    online.Add("number_of_checks", studio.AnzahlÜberprüfungen);
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
            var PersonID = GetStudioID<dboPerson>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            var tokenAktion = GetTokenAktionViaOnlineID(onlineID, action);
            tokenAktion.PersonID = PersonID;

            SimpleTransformToStudio<resPartnerFstoken, dboAktionOnlineToken>(
                onlineID,
                action,
                studioModel => studioModel.AktionsID,
                (online, studio) =>
                {
                    studio.Name = online.name;
                    // PersonID set on tokenAktion
                    studio.Ablaufdatum = online.expiration_date;
                    studio.LetzteBenutzungAmUm = online.last_datetime_of_use;
                    studio.ErsteBenutzungAmUm = online.first_datetime_of_use;
                    studio.AnzahlÜberprüfungen = online.number_of_checks;
                },
                tokenAktion,
                (a, aot) => aot.AktionsID = a.AktionsID);
        }

        private dboAktion GetTokenAktionViaOnlineID(int onlineID, TransformType action)
        {
            if (action == TransformType.CreateNew)
            {
                return new dboAktion()
                {
                    AktionstypID = 2005881, // OnlineToken
                    AktionsdetailtypID = 2300, // Out
                    zMarketingID = 0,
                    Durchführungstag = DateTime.Today,
                    Durchführungszeit = DateTime.Now.TimeOfDay,
                    Sachbearbeiter = Environment.UserName
                };
            }
            else
            {
                using (var db = MdbService.GetDataService<dboAktion>())
                {
                    return db.ExecuteQuery<dboAktion>(
                        "SELECT a.* FROM dbo.AktionOnlineToken at " +
                        "INNER JOIN dbo.Aktion a on at.AktionsID = a.AktionsID " +
                        "WHERE at.sosync_fso_id = @sosync_fso_id",
                        new { sosync_fso_id = onlineID }).SingleOrDefault();
                }
            }
        }
    }
}
