using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Flows
{
    [StudioModel(Name = "sosync.product_template")]
    [OnlineModel(Name = "product_template")]
    public class ProductTemplateFlow : UniformSyncFlow
    {
        #region Constructors
        public ProductTemplateFlow(IServiceProvider svc) : base(svc)
        {
            Fields.AddRange(new string[] {
                "id",
                "name",
                "company_id",
                "create_date",
                "write_date",
                "sosync_write_date"
                });
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
        #endregion
    }
}
