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
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.PersonGruppe")]
    [OnlineModel(Name = "frst.persongruppe")]
    public class PersonGruppeFlow
        : ReplicateSyncFlow
    {
        public PersonGruppeFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonGruppe>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonGruppe>())
            {
                var studioModel = db.Read(new { PersonGruppeID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zGruppeDetail", studioModel.zGruppeDetailID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID);

                if (studioModel.zVerzeichnisID.HasValue && studioModel.zVerzeichnisID > 0)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zVerzeichnis", studioModel.zVerzeichnisID.Value);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "zgruppedetail_id", "partner_id", "frst_zverzeichnis_id" });

            var odooGruppeDetailID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppedetail_id"])[0]);
            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0]);

            int? odoozVerzeichnisID = null;

            if (odooModel["frst_zverzeichnis_id"] != null)
                odoozVerzeichnisID = OdooConvert.ToInt32((string)((List<object>)odooModel["frst_zverzeichnis_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppedetail", odooGruppeDetailID.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", odooPartnerID.Value);

            if (odoozVerzeichnisID.HasValue && odoozVerzeichnisID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "frst.zverzeichnis", odoozVerzeichnisID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var zgruppedetail_id = 0;
            var partner_id = 0;
            int? frst_zverzeichnis_id = null;

            using (var db = MdbService.GetDataService<dboPersonGruppe>())
            {
                // Get the referenced Studio-IDs
                var personGruppe = db.Read(new { PersonGruppeID = studioID }).SingleOrDefault();

                // Get the corresponding Online-IDs
                zgruppedetail_id = GetOnlineID<dbozGruppeDetail>(
                    "dbo.zGruppeDetail",
                    "frst.zgruppedetail",
                    personGruppe.zGruppeDetailID)
                    .Value;

                partner_id = GetOnlineID<dboPerson>(
                    "dbo.Person",
                    "res.partner",
                    personGruppe.PersonID)
                    .Value;

                if (personGruppe.zVerzeichnisID.HasValue && personGruppe.zVerzeichnisID.Value > 0)
                {
                    frst_zverzeichnis_id = GetOnlineID<dboPerson>(
                        "dbo.zVerzeichnis",
                        "frst.zverzeichnis",
                        personGruppe.zVerzeichnisID.Value)
                        .Value;
                }
            }

            // Do the transformation
            SimpleTransformToOnline<dboPersonGruppe, frstPersongruppe>(
                studioID,
                action,
                studioModel => studioModel.PersonGruppeID,
                (studio, online) =>
                {
                    online.Add("zgruppedetail_id", zgruppedetail_id);
                    online.Add("partner_id", partner_id);
                    online.Add("frst_zverzeichnis_id", (object)frst_zverzeichnis_id ?? false);
                    online.Add("steuerung_bit", studio.Steuerung);
                    online.Add("gueltig_von", studio.GültigVon.Date);
                    online.Add("gueltig_bis", studio.GültigBis.Date);

                    online.Add("bestaetigt_typ", (object)MdbService
                        .GetTypeValue(studio.BestaetigungsTypID) ?? false);

                    online.Add("bestaetigt_am_um", DateTimeHelper.ToUtc(studio.BestaetigtAmUm));
                    online.Add("bestaetigt_herkunft", studio.BestaetigungsHerkunft);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "zgruppedetail_id", "partner_id", "frst_zverzeichnis_id" });

            var odooGruppeDetailID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppedetail_id"])[0])
                .Value;

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0])
                .Value;

            int? odoozVerzeichnisID = null;
            if (odooModel["frst_zverzeichnis_id"] != null)
                odoozVerzeichnisID = OdooConvert.ToInt32((string)((List<object>)odooModel["frst_zverzeichnis_id"])[0]).Value;

            // Get the corresponding Studio-IDs
            var zGruppeDetailID = GetStudioID<dbozGruppeDetail>(
                "frst.zgruppedetail",
                "dbo.zGruppeDetail",
                odooGruppeDetailID)
                .Value;

            var PersonID = GetStudioID<dboPerson>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            int? zVerzeichnisID = null;
            if (odoozVerzeichnisID.HasValue && odoozVerzeichnisID.Value > 0)
            {
                zVerzeichnisID = GetStudioID<dboPerson>(
                    "frst.zverzeichnis",
                    "dbo.zVerzeichnis",
                    odoozVerzeichnisID.Value)
                    .Value;
            }

            // Do the transformation
            SimpleTransformToStudio<frstPersongruppe, dboPersonGruppe>(
                onlineID,
                action,
                studioModel => studioModel.PersonGruppeID,
                (online, studio) =>
                    {
                        studio.zGruppeDetailID = zGruppeDetailID;
                        studio.PersonID = PersonID;
                        studio.zVerzeichnisID = zVerzeichnisID;
                        studio.Steuerung = online.steuerung_bit;
                        studio.GültigVon = online.gueltig_von.Date;
                        studio.GültigBis = online.gueltig_bis.Date;

                        studio.BestaetigungsTypID = MdbService
                            .GetTypeID("PersonGruppe_BestaetigungsTypID", online.bestaetigt_typ);

                        studio.BestaetigtAmUm = DateTimeHelper.ToLocal(online.bestaetigt_am_um);
                        studio.BestaetigungsHerkunft = online.bestaetigt_herkunft;
                        studio.Status = online.state;
                    });
        }
    }
}


