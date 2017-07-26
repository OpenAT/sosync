using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Models;
using WebSosync.Data.Models;
using dadi_data.Models;
using System.Linq;

namespace Syncer.Flows
{
    [StudioModel(Name = "dboxBPKAccount")]
    [OnlineModel(Name = "res.company")]
    public class CompanyFlow : SyncFlow
    {
        #region Constructors
        public CompanyFlow(IServiceProvider svc)
            : base(svc)
        {
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs required
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs required
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var dicCompany = Odoo.Client.GetDictionary("res.company", onlineID, new string[] { "id", "partner_id", "write_date" });
            var partnerID = Convert.ToInt32(((List<Object>)dicCompany["partner_id"])[0]);

            var dicPartner = Odoo.Client.GetDictionary("res.partner", partnerID, new string[] { "sosync_fs_id" });

            if (!string.IsNullOrEmpty((string)dicPartner["sosync_fs_id"]) && Convert.ToInt32(dicPartner["sosync_fs_id"]) > 0)
                return new ModelInfo(onlineID, int.Parse((string)dicPartner["sosync_fs_id"]), DateTime.Parse((string)dicCompany["write_date"]));
            else
                return new ModelInfo(onlineID, null, DateTime.Parse((string)dicCompany["write_date"]));
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            dboxBPKAccount acc = null;

            using (var db = Mdb.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();
            }

            return new ModelInfo(studioID, acc.res_company_id, acc.write_date);
        }

        protected override void TransformToOnline(int studioID)
        {
            // Do nothing, for now, to see if job gets closed successfully
        }

        protected override void TransformToStudio(int onlineID)
        {
            // Do nothing, for now, to see if job gets closed successfully
        }
        #endregion

        #region Methods
        #endregion
    }
}
