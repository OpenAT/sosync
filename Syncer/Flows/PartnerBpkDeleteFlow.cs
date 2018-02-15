using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using WebSosync.Data.Models;
using dadi_data.Models;
using System.Linq;
using Syncer.Exceptions;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonBPK")]
    [OnlineModel(Name = "res.partner.bpk")]
    public class PartnerBpkDeleteFlow : DeleteSyncFlow
    {
        public PartnerBpkDeleteFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs for delete
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs for delete
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException("Delete from fs to fso not supported.");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                var data = db.Read(new { PersonBPKID = Job.Sync_Target_Record_ID.Value }).SingleOrDefault();

                if (data == null)
                    throw new SyncerException($"Failed to read data from model {StudioModelName} {Job.Sync_Target_Record_ID.Value} before deletion.");

                UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(data));

                var query = $"delete from {StudioModelName} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id; select @@ROWCOUNT;";
                UpdateSyncTargetRequest($"-- @id = {Job.Sync_Target_Record_ID.Value}\n" + query);

                var affectedRows = db.ExecuteQuery<int>(query, new { id = Job.Sync_Target_Record_ID.Value }).SingleOrDefault();
                UpdateSyncTargetAnswer($"Deleted rows: {affectedRows}", null);

                if (affectedRows == 0)
                    throw new SyncerException($"Failed to delete model {StudioModelName} {Job.Sync_Target_Record_ID.Value}, no rows affected by the delete.");
            }
        }
    }
}
