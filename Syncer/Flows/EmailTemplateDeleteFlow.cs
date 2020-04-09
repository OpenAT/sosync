using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using WebSosync.Data.Models;
using dadi_data.Models;
using Syncer.Attributes;
using WebSosync.Data.Constants;
using System.Linq;
using Syncer.Exceptions;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using WebSosync.Common;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xTemplate")]
    [OnlineModel(Name = "email.template")]
    public class EmailTemplateDeleteFlow
        : DeleteSyncFlow
    {
        public EmailTemplateDeleteFlow(SyncServiceCollection svc)
            : base(svc)
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
            using (var db = Svc.MdbService.GetDataService<dboxTemplate>())
            {
                var template = db.Read(new { sosync_fso_id = onlineID })
                    .SingleOrDefault();

                if (template == null)
                    throw new SyncerException($"Failed to read data from model {StudioModelName} {Job.Sync_Target_Record_ID.Value} before deletion.");

                UpdateSyncTargetDataBeforeUpdate(Svc.Serializer.ToXML(template));

                var query = $"update {StudioModelName} set GültigBis = cast(dateadd(day, -1, getdate()) as date) where {Svc.MdbService.GetStudioModelIdentity(StudioModelName)} = @id; select @@ROWCOUNT;";
                UpdateSyncTargetRequest($"-- @id = {Job.Sync_Target_Record_ID.Value}\n" + query);

                template.GültigBis = DateTime.Today.AddDays(-1);

                db.Update(template);
            }
        }
    }
}
