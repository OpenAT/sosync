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
    [StudioModel(Name = "dbo.xFragebogenFrage")]
    [OnlineModel(Name = "survey.question")]
    public class SurveyQuestionFlow
        : ReplicateSyncFlow
    {
        public SurveyQuestionFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxFragebogenFrage>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<surveyQuestion>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "survey.survey", Convert.ToInt32(model.survey_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<surveyQuestion, dboxFragebogenFrage>(
                onlineID,
                action,
                studio => studio.xFragebogenFrageID,
                (online, studio) =>
                {
                    var surveyID = GetStudioIDFromOnlineReference(
                      "dbo.xFragebogen",
                      online,
                      x => x.survey_id,
                      true);

                    studio.xFragebogenID = surveyID.Value;
                    studio.Frage = online.question;
                    studio.FragetypID = Svc.TypeService.GetTypeID("xFragebogenFrage_FragetypID", online.type) ?? 0;
                    studio.Reihenfolge = online.sequence;

                    int? pageSequence = null;
                    var online_page_id = OdooConvert.ToInt32ForeignKey(online.page_id, true);

                    if (online_page_id.HasValue && online_page_id > 0)
                    {
                        var pageDict = Svc.OdooService.Client.GetDictionary(
                            "survey.page",
                            online_page_id.Value,
                            new[] { "sequence" });

                        if (pageDict != null && pageDict.ContainsKey("sequence") && pageDict["sequence"] != null)
                        {
                            pageSequence = Convert.ToInt32(pageDict["sequence"]);
                        }
                    }
                    studio.ReihenfolgeSeite = pageSequence;
                });
        }
    }
}
