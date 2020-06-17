using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_contact")]
    [OnlineModel(Name = "mail.mass_mailing.contact")]
    public class MailMassMailingContactMergeFlow
        : MergeSyncFlow
    {
        public MailMassMailingContactMergeFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

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
            throw new NotSupportedException();
        }
    }
}
