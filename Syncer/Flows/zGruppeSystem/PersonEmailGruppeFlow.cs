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
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.PersonEmailGruppe")]
    [OnlineModel(Name = "frst.personemailgruppe")]
    public class PersonEmailGruppeFlow
        : ReplicateSyncFlow
    {
        public PersonEmailGruppeFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService)
            : base(logger, odooService, conf, flowService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonEmailGruppe>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                var studioModel = db.Read(new { PersonEmailGruppeID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zGruppeDetail", studioModel.zGruppeDetailID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.PersonEmail", studioModel.PersonEmailID);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "zgruppedetail_id", "frst_personemail_id" });

            var odooGruppeDetailID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppedetail_id"])[0]);
            var odooPersonEmailID = OdooConvert.ToInt32((string)((List<object>)odooModel["frst_personemail_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppedetail", odooGruppeDetailID.Value);
            RequestChildJob(SosyncSystem.FSOnline, "frst.personemail", odooPersonEmailID.Value);
        }


        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var zgruppedetail_id = 0;
            var frst_personemail_id = 0;

            using (var db = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                // Get the referenced Studio-IDs
                var personEmailGruppe = db.Read(new { PersonEmailGruppeID = studioID }).SingleOrDefault();

                // Get the corresponding Online-IDs
                zgruppedetail_id = GetOnlineID<dbozGruppeDetail>(
                    "dbo.zGruppeDetail",
                    "frst.zgruppedetail",
                    personEmailGruppe.zGruppeDetailID)
                    .Value;

                frst_personemail_id = GetOnlineID<dboPersonEmail>(
                    "dbo.PersonEmail",
                    "frst.personemail",
                    personEmailGruppe.PersonEmailID)
                    .Value;
            }


            SimpleTransformToOnline<dboPersonEmailGruppe, frstPersonemailgruppe>(
                studioID,
                action,
                studioModel => studioModel.PersonEmailGruppeID,
                (studio, online) =>
                    {
                        online.Add("zgruppedetail_id", zgruppedetail_id);
                        online.Add("frst_personemail_id", frst_personemail_id);
                        online.Add("steuerung_bit", studio.Steuerung);
                        online.Add("gueltig_von", studio.GültigVon.Date);
                        online.Add("gueltig_bis", studio.GültigBis.Date);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "zgruppedetail_id", "frst_personemail_id" });

            var odooGruppeDetailID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppedetail_id"])[0])
                .Value;

            var odooPersonEmailID = OdooConvert.ToInt32((string)((List<object>)odooModel["frst_personemail_id"])[0])
                .Value;

            // Get the corresponding Studio-IDs
            var zGruppeDetailID = GetStudioID<dbozGruppeDetail>(
                "frst.zgruppedetail",
                "dbo.zGruppeDetail",
                odooGruppeDetailID)
                .Value;

            var PersonEmailID = GetStudioID<dboPersonEmail>(
                "frst.personemail",
                "dbo.PersonEmail",
                odooPersonEmailID)
                .Value;

            // Do the transformation
            SimpleTransformToStudio<frstPersonemailgruppe, dboPersonEmailGruppe>(
                onlineID,
                action,
                studioModel => studioModel.PersonEmailGruppeID,
                (online, studio) =>
                {
                    studio.zGruppeDetailID = zGruppeDetailID;
                    studio.PersonEmailID = PersonEmailID;
                    studio.Steuerung = online.steuerung_bit;
                    studio.GültigVon = online.gueltig_von.Date;
                    studio.GültigBis = online.gueltig_bis.Date;
                });
        }
    }
}

