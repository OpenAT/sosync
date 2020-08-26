using DaDi.Odoo.Models.GetResponse;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Syncer.Flows.GetResponse
{
    [StudioModel(Name = "fson.gr_tag")]
    [OnlineModel(Name = "gr.tag")]
    public class GrTagDeleteFlow
        : DeleteSyncFlow
    {
        public GrTagDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleDeleteInOnline<grTag>(studioID);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsongr_tag>(onlineID);
        }
    }
}
