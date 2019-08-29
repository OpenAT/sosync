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
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "frst.zgruppedetail")]
    public class zGruppeDetailFlow
        : ReplicateSyncFlow
    {
        public zGruppeDetailFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dbozGruppeDetail>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dbozGruppeDetail>())
            {
                var studioModel = db.Read(new { zGruppeDetailID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zGruppe", studioModel.zGruppeID);

                if (studioModel.BestaetigungxTemplateID.HasValue)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xTemplate", studioModel.BestaetigungxTemplateID.Value);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "zgruppe_id", "bestaetigung_email" });
            var odooGruppeID = OdooConvert.ToInt32ForeignKey(odooModel["zgruppe_id"], allowNull: false);
            var odooEmailID = OdooConvert.ToInt32ForeignKey(odooModel["bestaetigung_email"], allowNull: true);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppe", odooGruppeID.Value);

            if (odooEmailID.HasValue && odooEmailID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "email.template", odooEmailID.Value);
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

                        int? emailID = null;

                        if (studio.BestaetigungxTemplateID.HasValue && studio.BestaetigungxTemplateID > 0)
                        {
                            emailID = GetOnlineID<dboxTemplate>(
                                "dbo.xTemplate",
                                "email.template",
                                studio.BestaetigungxTemplateID.Value);
                        }

                        online.Add("zgruppe_id", odoozgruppeID.Value);
                        online.Add("gruppe_kurz", studio.GruppeKurz);
                        online.Add("gruppe_lang", studio.GruppeLang);
                        online.Add("gui_anzeigen", studio.GUIAnzeigen);
                        online.Add("gueltig_von", studio.GültigVon);
                        online.Add("gueltig_bis", studio.GültigBis);
                        online.Add("bestaetigung_erforderlich", studio.BestaetigungErforderlich);

                        online.Add("bestaetigung_typ", (object)MdbService
                            .GetTypeValue(studio.BestaetigungtypID) ?? false);

                        online.Add("bestaetigung_email", (object)emailID ?? false);

                        online.Add("bestaetigung_text", studio.BestaetigungText);
                        online.Add("bestaetigung_thanks", studio.BestaetigungDanke);
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
                    studio.BestaetigungErforderlich = online.bestaetigung_erforderlich ?? false;

                    studio.BestaetigungtypID = MdbService
                        .GetTypeID("zGruppeDetail_BestaetigungtypID", online.bestaetigung_typ);

                    int? xTemplateID = null;
                    if (online.bestaetigung_email != null)
                    {
                        xTemplateID = GetStudioID<dboxTemplate>(
                            "email.template",
                            "dbo.xTemplate",
                            Convert.ToInt32(online.bestaetigung_email[0]))
                            .Value;
                    }
                    studio.BestaetigungxTemplateID = xTemplateID;

                    studio.BestaetigungText = online.bestaetigung_text;
                    studio.BestaetigungDanke = online.bestaetigung_thanks;
                });
        }
    }
}
