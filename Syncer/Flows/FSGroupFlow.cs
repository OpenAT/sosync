using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using System;
using System.Linq;
using WebSosync.Data;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.zGruppeDetail")]
    [OnlineModel(Name = "fs.group")]
    public class FSGroupFlow : SyncFlow
    {
        #region Constructors
        public FSGroupFlow(IServiceProvider svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "fs.group");
            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dbozGruppeDetail>())
            {
                var group = db.Read(new { zGruppeDetailID = studioID }).SingleOrDefault();

#warning TODO: Add missing write dates and fso_id, after database has the fields
                return new ModelInfo(studioID, null, null, null);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // Has no child jobs
            //RequestChildJob(SosyncSystem.FSOnline, "fs.group", 0);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // Has no child jobs
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new System.NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new System.NotImplementedException();
        }
        #endregion
    }
}
