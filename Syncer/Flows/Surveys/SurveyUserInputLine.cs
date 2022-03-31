using DaDi.Odoo.Models.Surveys;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Linq;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows.Surveys
{
    [StudioModel(Name = "dbo.AktionFragebogenDetail")]
    [OnlineModel(Name = "survey.user_input_line")]
    [ConcurrencyOnlineWins]
    public class SurveyUserInputLine
        : ReplicateSyncFlow
    {
        public SurveyUserInputLine(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboAktionFragebogenDetail>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<surveyUserInputLine>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "survey.user_input", Convert.ToInt32(model.user_input_id[0]), SosyncJobSourceType.Default);
            RequestChildJob(SosyncSystem.FSOnline, "survey.question", Convert.ToInt32(model.question_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<surveyUserInputLine, dboAktionFragebogenDetail>(
                onlineID,
                action,
                studio => studio.AktionFragebogenDetailID,
                (online, studio) =>
                {
                    var aktionID = GetStudioIDFromOnlineReference(
                        "dbo.AktionFragebogen",
                        online,
                        x => x.user_input_id,
                        true);

                    var xFragebogenFrageID = GetStudioIDFromOnlineReference(
                        "dbo.xFragebogenFrage",
                        online,
                        x => x.question_id,
                        true);

                    studio.AktionsID = aktionID.Value;
                    studio.xFragebogenFrageID = xFragebogenFrageID;
                    studio.AntworttypID = Svc.TypeService.GetTypeID("AktionFragebogenDetail_AntworttypID", online.answer_type).Value;
                    studio.Übersprungen = online.skipped;
                    studio.QuizPunkte = online.quizz_mark;

                    studio.Wert = CreateAnswer(online); // Combine online values into single column
                });
        }

        private static string CreateAnswer(surveyUserInputLine inputLine)
        {
            if (inputLine.value_date != null)
                return inputLine.value_date.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff");

            if (inputLine.value_suggested != null || inputLine.value_suggested_row != null)
            {
                // Matrix answers are not supported, but if it happens render both fields
                var row1 = (string)(inputLine.value_suggested != null ? inputLine.value_suggested[1] : null);
                var row2 = (string)(inputLine.value_suggested_row != null ? inputLine.value_suggested_row[1] : null);
                return row1 ?? "" + (row1 != null && row2 != null ? "|" : "") + row2 ?? "";
            }

            // value_number is 0 instead of null, so process it last.
            // If all others are null use its value straight.
            return inputLine.value_free_text ?? inputLine.value_text ?? inputLine.value_number.Value.ToString("0");
        }
    }
}
