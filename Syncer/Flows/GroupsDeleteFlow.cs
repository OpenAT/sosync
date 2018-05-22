using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.res_groups")]
    [OnlineModel(Name = "res.groups")]
    public class GroupsDeleteFlow
        : DeleteSyncFlow
    {
        public GroupsDeleteFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        { }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs for groups
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs for groups
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var onlineGroup = OdooService.Client.GetModel<resGroups>(OnlineModelName, Job.Sync_Target_Record_ID.Value);

            if (onlineGroup == null)
                throw new SyncerException($"Failed to read data from model {OnlineModelName} {Job.Sync_Target_Record_ID.Value} before deletion.");

            UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

            try
            {
                OdooService.Client.UnlinkModel(OnlineModelName, Job.Sync_Target_Record_ID.Value);
            }
            finally
            {
                UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            using (var db = MdbService.GetDataService<fsonres_groups>())
            {
                var data = db.Read(new { res_groupsID = Job.Sync_Target_Record_ID.Value }).SingleOrDefault();

                if (data == null)
                    throw new SyncerException($"Failed to read data from model {StudioModelName} {Job.Sync_Target_Record_ID.Value} before deletion.");

                UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(data));

                var query = $"update {StudioModelName} set noSyncJobOnDeleteSwitch = 1 where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id; delete from {StudioModelName} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id; select @@ROWCOUNT;";
                UpdateSyncTargetRequest($"-- @id = {Job.Sync_Target_Record_ID.Value}\n" + query);

                var affectedRows = db.ExecuteQuery<int>(query, new { id = Job.Sync_Target_Record_ID.Value }).SingleOrDefault();
                UpdateSyncTargetAnswer($"Deleted rows: {affectedRows}", null);

                if (affectedRows == 0)
                    throw new SyncerException($"Failed to delete model {StudioModelName} {Job.Sync_Target_Record_ID.Value}, no rows affected by the delete.");
            }
        }
    }
}
