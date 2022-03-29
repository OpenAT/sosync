using DaDi.Odoo.Models.Surveys;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using WebSosync.Data;

namespace Syncer.Flows.Surveys
{
    [StudioModel(Name = "dbo.AktionFragebogen")]
    [OnlineModel(Name = "survey.user_input")]
    [ConcurrencyOnlineWins]
    public class SurveyUserInput
        : ReplicateSyncFlow
    {
        public SurveyUserInput(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboAktionFragebogen>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<surveyUserInput, dboAktionFragebogen>(
                onlineID,
                action,
                studio => studio.AktionsID,
                (online, studio) =>
                {
                    throw new NotImplementedException();
                });
        }
    }
}
