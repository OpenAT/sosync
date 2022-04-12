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
    [StudioModel(Name = "dbo.xFragebogen")]
    [OnlineModel(Name = "survey.survey")]
    [ConcurrencyOnlineWins]
    public class SurveySurveyFlow
        : ReplicateSyncFlow
    {
        public SurveySurveyFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxFragebogen>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<surveySurvey, dboxFragebogen>(
                onlineID,
                action,
                studio => studio.xFragebogenID,
                (online, studio) =>
                {
                    studio.Titel = online.title;
                    studio.Beschreibung = online.description;

                    if (action == TransformType.CreateNew)
                    {
                        studio.AnlageAmUm = DateTime.Now;
                    }
                });
        }
    }
}
