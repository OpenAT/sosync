using Syncer.Attributes;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dboPerson")]
    [OnlineModel(Name = "res.Partner")]
    public class PartnerFlow : SyncFlow
    {
        #region Constructors
        public PartnerFlow(IServiceProvider svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var dic = Odoo.Client.GetDictionary("res.partner", 1, new string[] { "id", "fs_id", "write_date" });

            if (!string.IsNullOrEmpty((string)dic["fs_id"]))
                return new ModelInfo(onlineID, int.Parse((string)dic["fs_id"]), DateTime.Parse((string)dic["write_date"]));
            else
                return new ModelInfo(onlineID, null, DateTime.Parse((string)dic["write_date"]));
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Read the foreign id from dboPerson

            // Read write date of
            // - dboPerson
            // - dboPersonAdresse
            // - dboPersonEmail
            // - ...

            // Return the foreign id and the most recent write date

            

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // Since this is a partner flow, onlineID represents the
            // res.partner id.

            // Use that partner id to get the company id
            var companyID = 0;

            // Tell the sync flow base, that this partner flow requires
            // the res.company
            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // Since this is a partner flow, onlineID represents the
            // dboPerson id.

            // Use the person id to get the xBPKAccount id
            var bpkAccount = 0;

            RequestChildJob(SosyncSystem.FundraisingStudio, "dboxBPKAccount", bpkAccount);
        }

        protected override void TransformToOnline(int studioID)
        {
            // Load studio model, save it to online
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID)
        {
            // Load online model, save it to studio
            throw new NotImplementedException();
        }
        #endregion
    }
}
