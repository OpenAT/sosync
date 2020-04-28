using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.PersonGruppe")]
    [OnlineModel(Name = "frst.persongruppe")]
    public class PersonGruppeMergeFlow
        : MergeSyncFlow
    {
        public PersonGruppeMergeFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            Svc.OdooService.Client.MergeModel(
                OnlineModelName,
                Job.Sync_Target_Record_ID.Value,
                Job.Sync_Target_Merge_Into_Record_ID.Value);

            RequestPostTransformChildJob(
                SosyncSystem.FundraisingStudio,
                StudioModelName,
                Job.Job_Source_Merge_Into_Record_ID.Value,
                true,
                SosyncJobSourceType.Default);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException(
                $"Merge for {OnlineModelName} is not supported.");
        }
    }
}
