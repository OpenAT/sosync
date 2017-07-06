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
        protected override DateTime? GetOnlineWriteDate(int id)
        {
            throw new NotImplementedException();
        }

        protected override DateTime? GetStudioWriteDate(int id)
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
