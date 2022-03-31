using DaDi.Odoo;
using DaDi.Odoo.Models.Surveys;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Data;
using WebSosync.Data.Constants;

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

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<surveyUserInput>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", Convert.ToInt32(model.partner_id[0]), SosyncJobSourceType.Default);
            RequestChildJob(SosyncSystem.FSOnline, "survey.survey", Convert.ToInt32(model.survey_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id", "create_date" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0]).Value;
            var odooCreate = OdooConvert.ToDateTime((string)odooModel["create_date"]).Value.ToLocalTime();

            // Get the corresponding Studio-IDs
            var PersonID = GetStudioID<dboPerson>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            var fragebogenAktion = GetTokenAktionViaOnlineID(onlineID, action, odooCreate);
            fragebogenAktion.PersonID = PersonID;

            SimpleTransformToStudio<surveyUserInput, dboAktionFragebogen>(
                onlineID,
                action,
                studio => studio.AktionsID,
                (online, studio) =>
                {
                    var surveyID = GetStudioIDFromOnlineReference(
                      "dbo.xFragebogen",
                      online,
                      x => x.survey_id,
                      true);

                    studio.xFragebogenID = surveyID.Value;
                    studio.StatustypID = Svc.TypeService.GetTypeID("AktionFragebogen_StatustypID", online.state);
                    studio.TestEintrag = online.test_entry;
                    studio.QuizPunkte = online.quizz_score;
                },
                fragebogenAktion,
                (online, aktionsID, af) => af.AktionsID = aktionsID);
        }

        private dboAktion GetTokenAktionViaOnlineID(int onlineID, TransformType action, DateTime onlineCreate)
        {
            if (action == TransformType.CreateNew)
            {
                return new dboAktion()
                {
                    AktionstypID = 2273, // Fragebogen
                    AktionsdetailtypID = 2305, // In
                    zMarketingID = 0,
                    Durchführungstag = onlineCreate.Date,
                    Durchführungszeit = onlineCreate.TimeOfDay,
                    Sachbearbeiter = Environment.UserName
                };
            }
            else
            {
                using (var db = Svc.MdbService.GetDataService<dboAktion>())
                {
                    return db.ExecuteQuery<dboAktion>(
                        "SELECT a.* FROM dbo.AktionFragebogen af " +
                        "INNER JOIN dbo.Aktion a on af.AktionsID = a.AktionsID " +
                        "WHERE af.sosync_fso_id = @sosync_fso_id",
                        new { sosync_fso_id = onlineID }).SingleOrDefault();
                }
            }
        }
    }
}
