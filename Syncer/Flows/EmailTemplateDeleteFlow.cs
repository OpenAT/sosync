using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using WebSosync.Data.Models;
using dadi_data.Models;
using Syncer.Attributes;
using WebSosync.Data.Constants;
using System.Linq;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xTemplate")]
    [OnlineModel(Name = "email.template")]
    public class EmailTemplateDeleteFlow
        : DeleteSyncFlow
    {
        public EmailTemplateDeleteFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException(
                $"Cannot delete {StudioModelName} ({studioID}). Hard delete is not supported.");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Only do a soft delete on xTemplate 
            using (var db = MdbService.GetDataService<dboxTemplate>())
            {
                var template = db.Read(new { sosync_fso_id = onlineID })
                    .SingleOrDefault();

                template.GültigBis = DateTime.Today.AddDays(-1);

                db.Update(template);
            }
        }
    }
}
