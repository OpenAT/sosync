using DaDi.Odoo;
using DaDi.Odoo.Models.Surveys;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows.Surveys
{
    [StudioModel(Name = "dbo.xFragebogenFrageAntwort")]
    [OnlineModel(Name = "survey.label")]
    [ConcurrencyOnlineWins]
    public class SurveyLabelFlow
        : ReplicateSyncFlow
    {
        public SurveyLabelFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxFragebogenFrageAntwort>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<surveyLabel>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "survey.question", Convert.ToInt32(model.question_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<surveyLabel, dboxFragebogenFrageAntwort>(
                onlineID,
                action,
                studio => studio.xFragebogenFrageID,
                (online, studio) =>
                {
                    var questionID = GetStudioIDFromOnlineReference(
                      "dbo.xFragebogenFrage",
                      online,
                      x => x.question_id,
                      true);

                    studio.xFragebogenFrageID = questionID.Value;
                    studio.Reihenfolge = online.sequence;
                    studio.Wert = online.value;
                    studio.QuizPunkte = online.quizz_mark;
                });
        }
    }
}
