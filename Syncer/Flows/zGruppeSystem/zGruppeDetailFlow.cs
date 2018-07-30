using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
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
            using (var db = MdbService.GetDataService<dbozGruppe>())
            {
                var studioModel = db.Read(new { zGruppeID = studioID }).SingleOrDefault();
                RequestChildJob(SosyncSystem.FundraisingStudio, StudioModelName, studioModel.zGruppeID);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "zgruppe_id" });
            var frstzGruppeID = OdooConvert.ToInt32((string)((List<object>)odooModel["zgruppe_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppe", frstzGruppeID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            if (action == TransformType.CreateNew)
                throw new SyncerException($"{StudioModelName} can only be created from FS, not from FS-Online.");

            throw new NotImplementedException();
        }
    }
}
