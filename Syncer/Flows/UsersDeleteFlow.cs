using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Attributes;
using Syncer.Enumerations;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.res_users")]
    [OnlineModel(Name = "res.users")]
    public class UsersDeleteFlow
        : DeleteSyncFlow
    {
        public UsersDeleteFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        { }

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
