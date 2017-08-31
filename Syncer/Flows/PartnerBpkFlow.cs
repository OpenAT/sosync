using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using dadi_data.Models;
using System.Linq;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonBPK")]
    [OnlineModel(Name = "res.partner.bpk")]
    public class PartnerBpkFlow : SyncFlow
    {
        public PartnerBpkFlow(IServiceProvider svc)
            : base(svc)
        { }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            return GetDefaultOnlineModelInfo(onlineID, "res.partner.bpk");
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                var bpk = db.Read(new { PersonBPKID = studioID }).SingleOrDefault();

#warning TODO: Replace res_partner_bpk_id with sosync_fso_id and use sosync_write_date, once database is updated
                if (bpk != null)
                    return new ModelInfo(studioID, bpk.res_partner_bpk_id, bpk.write_date, bpk.write_date);
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            throw new NotImplementedException();
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotImplementedException();
        }
    }
}
