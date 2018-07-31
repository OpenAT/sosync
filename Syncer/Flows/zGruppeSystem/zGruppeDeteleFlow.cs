﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeDeteleFlow
        : DeleteSyncFlow
    {
        public zGruppeDeteleFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
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
            var id = 0;

            if (Job.Job_Source_Target_Record_ID.HasValue && Job.Job_Source_Target_Record_ID > 0)
                id = Job.Job_Source_Target_Record_ID.Value;
            else
                id = GetOnlineIDFromOdooViaStudioID(OnlineModelName, Job.Job_Source_Record_ID).Value;

            var data = OdooService.Client.GetModel<frstzGruppe>(OnlineModelName, id);

            if (data == null)
                throw new SyncerException($"Failed to read data from model {OnlineModelName} {Job.Sync_Target_Record_ID.Value} before deletion.");

            UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

            OdooService.Client.UnlinkModel(OnlineModelName, id);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // zGruppe cannot be deleted. GUIAnzeigen = false should be used instead.
            throw new SyncerException($"Model {StudioModelName} can only be deleted from FS, not from FS-Online.");

            /*
            using (var db = MdbService.GetDataService<dbozGruppe>())
            {
                var data = db.Read(new { zGruppeID = Job.Sync_Target_Record_ID.Value })
                    .SingleOrDefault();

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
            */
        }
    }
}
