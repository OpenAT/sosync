using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "frst.zgruppedetail")]
    public class zGruppeDetailFlow
        : ReplicateSyncFlow
    {
        public zGruppeDetailFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
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
                    grpDetail.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, grpDetail.zGruppeDetailID);

                odoozgruppeID = GetFsoIdByFsId("frst.zgruppe", grpDetail.zGruppeID).Value;
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
                        online.Add("gueltig_von", studio.GültigVon.Date);
                        online.Add("gueltig_bis", studio.GültigBis.Date);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new SyncerException($"{StudioModelName} can only be created/updated from FS, not from FS-Online.");

            //if (action == TransformType.CreateNew)
            //    throw new SyncerException($"{StudioModelName} can only be created from FS, not from FS-Online.");

            //var odooGrp = OdooService.Client.GetModel<frstzGruppedetail>(OnlineModelName, onlineID);

            //if (!IsValidFsID(odooGrp.Sosync_FS_ID))
            //    odooGrp.Sosync_FS_ID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

            //var zGruppeID = GetFsIdByFsoId("dbo.zGruppe", "zGruppeID", odooGrp.zgruppe_id).Value;

            //SimpleTransformToStudio<frstzGruppedetail, dbozGruppeDetail>(
            //    onlineID,
            //    action,
            //    studioModel => studioModel.zGruppeDetailID,
            //    (online, studio) =>
            //        {
            //            studio.zGruppeID = zGruppeID;
            //            studio.GruppeKurz = online.gruppe_kurz;
            //            studio.GruppeLang = online.gruppe_lang;
            //            studio.GUIAnzeigen = online.gui_anzeigen;
            //        });
        }
    }
}
