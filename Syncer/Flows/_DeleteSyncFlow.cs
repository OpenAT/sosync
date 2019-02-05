using DaDi.Odoo.Models;
using dadi_data.Interfaces;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    public abstract class DeleteSyncFlow : SyncFlow
    {
        public DeleteSyncFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            CheckRunCount(MaxRunCount);

            if (Job.Job_Source_System == SosyncSystem.FundraisingStudio)
                SetDeleteInfos(OnlineModelName, Job);
            else
                SetDeleteInfos(StudioModelName, Job);

            Stopwatch consistencyWatch = new Stopwatch();

            SetupChildJobRequests();
            HandleChildJobs(
                "Child Job",
                RequiredChildJobs, 
                flowService, 
                null, 
                consistencyWatch,
                ref requireRestart, 
                ref restartReason);

            var description = $"Deleting [{Job.Sync_Target_System}] {Job.Sync_Target_Model} {Job.Sync_Target_Record_ID} (Source: [{Job.Sync_Source_System}] {Job.Sync_Source_Model} {Job.Sync_Source_Record_ID})";
            HandleTransformation(description, null, consistencyWatch, ref requireRestart, ref restartReason);

            HandleChildJobs(
                "Post Transformation Child Job",
                RequiredPostTransformChildJobs,
                flowService,
                null,
                consistencyWatch,
                ref requireRestart,
                ref restartReason);
        }

        private void SetDeleteInfos(string modelName, SyncJob job)
        {
            using (var db = GetDb())
            {
                if (job.Job_Source_System == SosyncSystem.FSOnline)
                {
                    job.Sync_Source_System = SosyncSystem.FSOnline;
                    job.Sync_Target_System = SosyncSystem.FundraisingStudio;

                    job.Sync_Source_Model = OnlineModelName;
                    job.Sync_Target_Model = StudioModelName;

                    var sourceOnlineID = job.Job_Source_Record_ID; 
                    var targetStudioID = GetStudioIDFromMssqlViaOnlineID(modelName, MdbService.GetStudioModelIdentity(StudioModelName), sourceOnlineID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceOnlineID;
                    job.Sync_Target_Record_ID = targetStudioID;

                    UpdateJob(Job, "Updating Delete-IDs");
                }
                else
                {
                    job.Sync_Source_System = SosyncSystem.FundraisingStudio;
                    job.Sync_Target_System = SosyncSystem.FSOnline;

                    job.Sync_Source_Model = StudioModelName;
                    job.Sync_Target_Model = OnlineModelName;

                    var sourceStudioID = job.Job_Source_Record_ID;
                    var targetOnlineID = GetOnlineIDFromOdooViaStudioID(modelName, sourceStudioID) ?? job.Job_Source_Target_Record_ID;

                    job.Sync_Source_Record_ID = sourceStudioID;
                    job.Sync_Target_Record_ID = targetOnlineID;

                    UpdateJob(Job, "Updating Delete-IDs");
                }
            }
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            // Not applicable for delete flows
            return null;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // Not applicable for delete flows
            return null;
        }

        protected void SimpleDeleteInOnline<TOdoo>(
            int studioID
            )
            where TOdoo : SosyncModelBase
        {
            int? odooID;
            if (Job.Job_Source_Target_Record_ID.HasValue && Job.Job_Source_Target_Record_ID > 0)
                odooID = Job.Job_Source_Target_Record_ID;
            else
                odooID = GetOnlineIDFromOdooViaStudioID(
                    OnlineModelName,
                    Job.Job_Source_Record_ID);

            TOdoo data = null;

            if (odooID.HasValue)
                data = OdooService.Client.GetModel<TOdoo>(OnlineModelName, odooID.Value);

            if (data == null)
                throw new SyncerException(String.Format("Failed to read data from model {0} {1} before deletion.{2}",
                    OnlineModelName,
                    odooID?.ToString() ?? "<Unknown ID>",
                    odooID.HasValue ? "" : " job_source_target_record_id was not set and the model could not be found via FS-ID."));

            UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

            OdooService.Client.UnlinkModel(OnlineModelName, odooID.Value);
        }

        protected void SimpleDeleteInStudio<TStudio>(
            int onlineID
            )
            where TStudio : MdbModelBase, ISosyncable, new()
        {

            int? studioID;
            if (Job.Job_Source_Target_Record_ID.HasValue && Job.Job_Source_Target_Record_ID > 0)
                studioID = Job.Job_Source_Target_Record_ID;
            else
                studioID = GetStudioIDFromMssqlViaOnlineID(
                    StudioModelName,
                    MdbService.GetStudioModelIdentity(StudioModelName),
                    onlineID);

            using (var db = MdbService.GetDataService<TStudio>())
            {
                var data = db.Read(
                    $"select * from {StudioModelName} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id;",
                    new { id = studioID })
                    .SingleOrDefault();

                if (data == null)
                    throw new SyncerException($"Failed to read data from model {StudioModelName} {studioID} before deletion.");

                UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(data));

                var query = $"update {StudioModelName} set noSyncJobOnDeleteSwitch = 1 where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id; "
                    + $"delete from {StudioModelName} where {MdbService.GetStudioModelIdentity(StudioModelName)} = @id; select @@ROWCOUNT;";

                UpdateSyncTargetRequest($"-- @id = {studioID}\n" + query);

                var affectedRows = db.ExecuteQuery<int>(query, new { id = studioID }).SingleOrDefault();
                UpdateSyncTargetAnswer($"Deleted rows: {affectedRows}", null);

                if (affectedRows == 0)
                    throw new SyncerException($"Failed to delete model {StudioModelName} {studioID}, no rows affected by the delete.");
            }
        }
    }
}
