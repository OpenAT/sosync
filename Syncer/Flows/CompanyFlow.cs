﻿using Syncer.Attributes;
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
            // Get company ids and write date
            var dicCompany = Odoo.Client.GetDictionary("res.company", onlineID, new string[] { "id", "partner_id", "write_date" });
            var partnerID = Convert.ToInt32(((List<Object>)dicCompany["partner_id"])[0]);

            // A company is a partner, so to get the sosync_fs_id for the company, the corresponding partner is needed
            var dicPartner = Odoo.Client.GetDictionary("res.partner", partnerID, new string[] { "sosync_fs_id" });

            // Pick sosync_fs_id from the partner entry of the company, and the write date from the company itself
            var fsIdStr = (string)dicPartner["sosync_fs_id"];
            var writeDateStr = (string)dicCompany["write_date"];

            if (!string.IsNullOrEmpty(fsIdStr) && Convert.ToInt32(fsIdStr) > 0)
                return new ModelInfo(onlineID, Convert.ToInt32(fsIdStr), DateTime.Parse(writeDateStr));
            else
                return new ModelInfo(onlineID, null, DateTime.Parse((string)dicCompany["write_date"]));
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // A company in FS is just an entry in the xBPKAccount table.
            // So get the write date from that table
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
            //throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID)
        {
            // Do nothing, for now, to see if job gets closed successfully
            //throw new NotImplementedException();
        }
        #endregion

        #region Methods
        #endregion
    }
}
