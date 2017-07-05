using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
