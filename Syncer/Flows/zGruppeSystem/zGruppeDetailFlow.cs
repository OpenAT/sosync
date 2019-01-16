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
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "frst.zgruppedetail")]
    public class zGruppeDetailFlow
        : ReplicateSyncFlow
    {
        public zGruppeDetailFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService)
            : base(logger, odooService, conf, flowService)
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
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "zgruppe_id" });
            var odooGruppeID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppe_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppe", odooGruppeID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var odoozgruppeID = 0;
            dbozGruppeDetail grpDetail = null;
            using (var db = MdbService.GetDataService<dbozGruppeDetail>())
            {
                grpDetail = db.Read(new { zGruppeDetailID = studioID }).SingleOrDefault();

                if (!grpDetail.sosync_fso_id.HasValue)
                    grpDetail.sosync_fso_id = GetOnlineIDFromOdooViaStudioID(OnlineModelName, grpDetail.zGruppeDetailID);

                odoozgruppeID = GetOnlineIDFromOdooViaStudioID("frst.zgruppe", grpDetail.zGruppeID).Value;
            }

            SimpleTransformToOnline<dbozGruppeDetail, frstzGruppedetail>(
                studioID,
                action,
                studioModel => studioModel.zGruppeDetailID,
                (studio, online) =>
                    {
                        online.Add("zgruppe_id", Convert.ToString(odoozgruppeID));
                        online.Add("gruppe_kurz", studio.GruppeKurz);
                        online.Add("gruppe_lang", studio.GruppeLang);
                        online.Add("gui_anzeigen", studio.GUIAnzeigen);
                        online.Add("gueltig_von", studio.GültigVon);
                        online.Add("gueltig_bis", studio.GültigBis);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new SyncerException($"{StudioModelName} can only be created/updated from FS, not from FS-Online.");
        }
    }
}
