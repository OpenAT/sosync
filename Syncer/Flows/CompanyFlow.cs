using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Models;
using WebSosync.Data.Models;

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
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            throw new NotImplementedException();
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            throw new NotImplementedException();
        }

        protected override void ConfigureOnlineToStudio(SyncJob sourceJob)
        {
            // No requirements
        }

        protected override void ConfigureStudioToOnline(SyncJob sourceJob)
        {
            // No requirements
        }
        #endregion
    }
}
