using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using WebSosync.Data;

namespace Syncer.Flows.Surveys
{
    [StudioModel(Name = "dbo.xFragebogenFrage")]
    [OnlineModel(Name = "survey.question")]
    public class SurveyQuestionDeleteFlow
        : DeleteSyncFlow
    {
        public SurveyQuestionDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<dboxFragebogenFrage>(onlineID);
        }
    }
}
