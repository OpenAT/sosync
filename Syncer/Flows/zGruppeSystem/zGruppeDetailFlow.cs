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

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "frst.zgruppedetail")]
    [SyncTargetStudio, SyncTargetOnline]
    public class zGruppeDetailFlow
        : ReplicateSyncFlow
    {
        public zGruppeDetailFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dbozGruppeDetail>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<dbozGruppeDetail>())
            {
                var studioModel = db.Read(new { zGruppeDetailID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zGruppe", studioModel.zGruppeID, SosyncJobSourceType.Default);

                if (studioModel.BestaetigungxTemplateID.HasValue)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xTemplate", studioModel.BestaetigungxTemplateID.Value, SosyncJobSourceType.Default);

                if (studioModel.zVerzeichnisID.HasValue)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zVerzeichnis", studioModel.zVerzeichnisID.Value, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[]
                {
                    "zgruppe_id",
                    "bestaetigung_email",
                    "bestaetigung_success_email",
                    "subscription_email",
                    "frst_zverzeichnis_id"
                });


            var odooGruppeID = OdooConvert.ToInt32ForeignKey(odooModel["zgruppe_id"], allowNull: false);

            var bestaetigungEmailID = OdooConvert.ToInt32ForeignKey(odooModel["bestaetigung_email"], allowNull: true);
            var bestaetigungSuccessEmailID = OdooConvert.ToInt32ForeignKey(odooModel["bestaetigung_success_email"], allowNull: true);
            var subscriptionEmailID = OdooConvert.ToInt32ForeignKey(odooModel["subscription_email"], allowNull: true);

            var odooVerzeichnisID = OdooConvert.ToInt32ForeignKey(odooModel["frst_zverzeichnis_id"], true);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppe", odooGruppeID.Value, SosyncJobSourceType.Default);

            if (bestaetigungEmailID.HasValue && bestaetigungEmailID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "email.template", bestaetigungEmailID.Value, SosyncJobSourceType.Default);

            if (bestaetigungSuccessEmailID.HasValue && bestaetigungSuccessEmailID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "email.template", bestaetigungSuccessEmailID.Value, SosyncJobSourceType.Default);

            if (subscriptionEmailID.HasValue && subscriptionEmailID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "email.template", subscriptionEmailID.Value, SosyncJobSourceType.Default);

            if (odooVerzeichnisID.HasValue && odooVerzeichnisID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "frst.zverzeichnis", odooVerzeichnisID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<dbozGruppeDetail, frstzGruppedetail>(
                studioID,
                action,
                studioModel => studioModel.zGruppeDetailID,
                (studio, online) =>
                    {
                        var odoozgruppeID = GetOnlineID<dbozGruppe>(
                                "dbo.zGruppe",
                                "frst_zgruppe",
                                studio.zGruppeID);

                        online.Add("zgruppe_id", odoozgruppeID.Value);

                        int? bestaetigungEmailID = null;
                        if (studio.BestaetigungxTemplateID.HasValue && studio.BestaetigungxTemplateID > 0)
                        {
                            bestaetigungEmailID = GetOnlineID<dboxTemplate>(
                                "dbo.xTemplate",
                                "email.template",
                                studio.BestaetigungxTemplateID.Value);
                            
                            if (bestaetigungEmailID == null)
                                throw new SyncerException($"email.template (bestaetigung_email) not found for dbo.xTemplate ({studio.BestaetigungxTemplateID})");
                        }
                        online.Add("bestaetigung_email", (object)bestaetigungEmailID ?? false);

                        int? bestaetigungSuccessEmailID = null;
                        if (studio.BestaetigungErfolgxTemplateID.HasValue && studio.BestaetigungErfolgxTemplateID > 0)
                        {
                            bestaetigungSuccessEmailID = GetOnlineID<dboxTemplate>(
                                "dbo.xTemplate",
                                "email.template",
                                studio.BestaetigungErfolgxTemplateID.Value);

                            if (bestaetigungSuccessEmailID == null)
                                throw new SyncerException($"email.template (bestaetigung_success_email) not found for dbo.xTemplate ({studio.BestaetigungErfolgxTemplateID})");
                        }
                        online.Add("bestaetigung_success_email", (object)bestaetigungSuccessEmailID ?? false);

                        int? subscribedEmailID = null;
                        if (studio.AnmeldungxTemplateID.HasValue && studio.AnmeldungxTemplateID > 0)
                        {
                            subscribedEmailID = GetOnlineID<dboxTemplate>(
                                "dbo.xTemplate",
                                "email.template",
                                studio.AnmeldungxTemplateID.Value);

                            if (subscribedEmailID == null)
                                throw new SyncerException($"email.template (subscription_email) not found for dbo.xTemplate ({studio.AnmeldungxTemplateID})");
                        }
                        online.Add("subscription_email", (object)subscribedEmailID ?? false);

                        int? verzeichnisID = null;
                        if (studio.zVerzeichnisID.HasValue && studio.zVerzeichnisID > 0)
                        {
                            verzeichnisID = GetOnlineID<dbozVerzeichnis>(
                                "dbo.zVerzeichnis",
                                "frst.zverzeichnis",
                                studio.zVerzeichnisID.Value);

                            if (verzeichnisID == null)
                                throw new SyncerException($"frst.zverzeichnis not found for dbo.zVerzeichnis ({studio.zVerzeichnisID})");
                        }
                        online.Add("frst_zverzeichnis_id", (object)verzeichnisID ?? false);
                        online.Add("geltungsbereich", studio.GeltungsBereich == 0 ? "system" : "local");

                        online.Add("gruppe_kurz", studio.GruppeKurz);
                        online.Add("gruppe_lang", studio.GruppeLang);
                        online.Add("gui_anzeigen", studio.GUIAnzeigen);
                        online.Add("gui_anzeige_profil", studio.GUIAnzeigeProfil);
                        online.Add("gueltig_von", studio.GültigVon);
                        online.Add("gueltig_bis", studio.GültigBis);
                        online.Add("bestaetigung_erforderlich", studio.BestaetigungErforderlich);

                        online.Add("bestaetigung_typ", (object)Svc.TypeService
                            .GetTypeValue(studio.BestaetigungtypID) ?? false);

                        online.Add("bestaetigung_text", studio.BestaetigungText);
                        online.Add("bestaetigung_thanks", studio.BestaetigungDanke);
                        online.Add("sync_with_getresponse", studio.SyncMitGetresponse);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<frstzGruppedetail, dbozGruppeDetail>(
                onlineID,
                action,
                studioModel => studioModel.zGruppeDetailID,
                (online, studio) =>
                {
                    var zGruppeID = GetStudioID<dbozGruppe>(
                        "frst.zgruppe",
                        "dbo.zGruppe",
                        Convert.ToInt32(online.zgruppe_id[0]))
                        .Value;
                    studio.zGruppeID = zGruppeID;

                    var bestaetigungxTemplateID = GetStudioIDFromOnlineReference(
                        "dbo.xTemplate",
                        online,
                        x => x.bestaetigung_email,
                        false);
                    studio.BestaetigungxTemplateID = bestaetigungxTemplateID;

                    var bestaetigungSuccessxTemplateID = GetStudioIDFromOnlineReference(
                        "dbo.xTemplate",
                        online,
                        x => x.bestaetigung_success_email,
                        false);
                    studio.BestaetigungErfolgxTemplateID = bestaetigungSuccessxTemplateID;

                    var anmeldungxTemplateID = GetStudioIDFromOnlineReference(
                        "dbo.xTemplate",
                        online,
                        x => x.subscription_email,
                        false);
                    studio.AnmeldungxTemplateID = anmeldungxTemplateID;

                    int? zVerzeichnisID = null;
                    if (online.frst_zverzeichnis_id != null)
                    {
                        zVerzeichnisID = GetStudioID<dbozVerzeichnis>(
                            "frst.zverzeichnis",
                            "dbo.zVerzeichnis",
                            Convert.ToInt32(online.frst_zverzeichnis_id[0]))
                            .Value;

                        if (zVerzeichnisID == null)
                            throw new SyncerException($"dbo.zVerzeichnis not found for frst.zverzeichnis ({online.frst_zverzeichnis_id[0]})");
                    }
                    studio.zVerzeichnisID = zVerzeichnisID;

                    studio.GruppeKurz = online.gruppe_kurz;
                    studio.GruppeLang = online.gruppe_lang;
                    studio.GUIAnzeigen = online.gui_anzeigen;
                    studio.GUIAnzeigeProfil = online.gui_anzeige_profil;
                    studio.GültigVon = online.gueltig_von;
                    studio.GültigBis = online.gueltig_bis;
                    studio.GeltungsBereich = (byte)(online.geltungsbereich == "system" ? 0 : 1);

                    studio.BestaetigungErforderlich = online.bestaetigung_erforderlich ?? false;

                    studio.BestaetigungtypID = Svc.TypeService
                        .GetTypeID("zGruppeDetail_BestaetigungtypID", online.bestaetigung_typ);

                    studio.BestaetigungText = online.bestaetigung_text;
                    studio.BestaetigungDanke = online.bestaetigung_thanks;
                    studio.SyncMitGetresponse = online.sync_with_getresponse;
                });
        }
    }
}
