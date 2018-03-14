using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using WebSosync.Data.Models;
using Syncer.Attributes;

namespace Syncer.Flows
{
    [DisableFlow()]
    public class PartnerDonationReportCorrectionFlow : TempSyncFlow
    {
        #region Constructors
        public PartnerDonationReportCorrectionFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }
        #endregion

        #region Methods
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
        #endregion
    }
}
