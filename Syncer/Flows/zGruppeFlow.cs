using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeFlow
        : ReplicateSyncFlow
    {
        public zGruppeFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, OnlineModelName);

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId(
                    StudioModelName,
                    MdbService.GetStudioModelIdentity(StudioModelName),
                    onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dbozGruppe>())
            {
                var zGrp = db.Read(new { zGruppeID = studioID }).SingleOrDefault();
                if (zGrp != null)
                {
                    if (!zGrp.sosync_fso_id.HasValue)
                        zGrp.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, zGrp.zGruppeID);

                    return new ModelInfo(studioID, zGrp.sosync_fso_id, zGrp.sosync_write_date, zGrp.write_date);
                }
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs required
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs required
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dbozGruppe studioModel = null;
            using (var db = MdbService.GetDataService<dbozGruppe>())
            {
                studioModel = db.Read(new { zGruppeID = studioID }).SingleOrDefault();

                if (!studioModel.sosync_fso_id.HasValue)
                    studioModel.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, studioModel.zGruppeID);

                UpdateSyncSourceData(Serializer.ToXML(studioModel));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>()
                {
                    { "tabellentyp_id", studioModel.TabellentypID },
                    { "gruppe_kurz", studioModel.GruppeKurz },
                    { "gruppe_lang", studioModel.GruppeLang },
                    { "gui_anzeigen", studioModel.GUIAnzeigen },
                    { "sosync_write_date", (studioModel.sosync_write_date ?? studioModel.write_date.ToUniversalTime()) }
                };

                if (action == TransformType.CreateNew)
                {
                    //data.Add("sosync_fs_id", acc.xBPKAccountID);
                    //int odooCompanyId = 0;
                    //try
                    //{
                    //    var userDic = OdooService.Client.GetDictionary("res.users", OdooService.Client.UserID, new string[] { "company_id" });
                    //    odooCompanyId = OdooService.Client.CreateModel("res.company", data, false);

                    //    acc.sosync_fso_id = odooCompanyId;
                    //    db.Update(acc);
                    //}
                    //finally
                    //{
                    //    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    //    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooCompanyId);
                    //}
                }
                else
                {
                    //var company = OdooService.Client.GetModel<resCompany>("res.company", acc.sosync_fso_id.Value);

                    //UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                    //try
                    //{
                    //    OdooService.Client.UpdateModel("res.company", data, acc.sosync_fso_id.Value, false);
                    //}
                    //finally
                    //{
                    //    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    //    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
                    //}
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotImplementedException();
        }
    }
}
