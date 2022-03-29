using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using WebSosync.Data;

namespace Syncer.Flows.Surveys
{
    [StudioModel(Name = "dbo.AktionFragebogenDetail")]
    [OnlineModel(Name = "survey.user_input_line")]
    public class SurveyUserInputLineDeleteFlow
        : DeleteSyncFlow
    {
        public SurveyUserInputLineDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboAktionFragebogenDetail>(onlineID);
        }
    }
}
