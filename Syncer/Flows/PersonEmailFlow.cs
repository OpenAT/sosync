using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonEmail")]
    [OnlineModel(Name = "frst.personemail")]
    [ModelPriority(4500)]
    public class PersonEmailFlow
        : ReplicateSyncFlow
    {
        public PersonEmailFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonEmail>(studioID);
        }

        protected override int? MatchInOnlineViaData(int studioID)
        {
            int? onlineID = null;
            string mail;
            int? partnerID;

            FetchStudioData(studioID, out mail, out partnerID);

            if (string.IsNullOrEmpty(mail))
                return null;

            var searchArgs = new[] {
                new OdooSearchArgument("partner_id", "=", partnerID.Value),
                new OdooSearchArgument("email", "=ilike", mail)
            };

            onlineID = Svc.OdooService.Client.SearchBy(OnlineModelName, searchArgs)
                .SingleOrDefault();

            return onlineID;
        }

        private void FetchStudioData(int studioID, out string mail, out int? partnerID)
        {
            mail = null;
            partnerID = null;

            using (var db = Svc.MdbService.GetDataService<dboPersonEmail>())
            {
                var personEmail = db.Read(new { PersonEmailID = studioID })
                    .SingleOrDefault();

                if (personEmail != null)
                {
                    mail = EmailHelper.MergeEmail(personEmail.EmailVor, personEmail.EmailNach);

                    partnerID = db.ExecuteQuery<int?>(
                        "SELECT sosync_fso_id FROM dbo.Person WHERE PersonID = @id",
                        new { id = personEmail.PersonID })
                        .SingleOrDefault();

                    if (string.IsNullOrEmpty(mail) || !partnerID.HasValue)
                        throw new SyncerException($"Cannot {nameof(MatchInOnlineViaData)}: E-Mail = '{mail}', partner_id = {partnerID}");
                }
                else
                {
                    throw new SyncerException($"Model {StudioModelName} ({studioID}) was not found in {SosyncSystem.FundraisingStudio.Value} while matching");
                }
            }
        }

        protected override int? MatchInStudioViaData(int onlineID)
        {
            int? studioID = null;

            var frstPersonemail = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new[] { "partner_id", "email" });

            if (frstPersonemail.ContainsKey("email"))
            {
                var mail = (string)frstPersonemail["email"];
                int partnerID = Convert.ToInt32(((List<object>)frstPersonemail["partner_id"])[0]);

                var resPartner = Svc.OdooService.Client.GetDictionary("res.partner", partnerID, new[] { "sosync_fs_id" });
                int? personID = null;

                if (resPartner.ContainsKey("sosync_fs_id"))
                    personID = Convert.ToInt32(resPartner["sosync_fs_id"]);

                if (string.IsNullOrEmpty(mail) || !personID.HasValue)
                    throw new SyncerException($"Cannot {nameof(MatchInStudioViaData)}: E-Mail = '{mail}', PersonID = {partnerID}");

                using (var db = Svc.MdbService.GetDataService<dboTypen>())
                {
                    studioID = db.ExecuteQuery<int?>(
                        $"SELECT {Svc.MdbService.GetStudioModelIdentity(StudioModelName)} FROM {StudioModelName} " +
                        "WHERE PersonID = @personID AND ISNULL(EmailVor, '') + '@' + ISNULL(EmailNach, '') = @mail",
                        new { personID, mail })
                        .SingleOrDefault();
                }
            }
            else
            {
                throw new SyncerException($"Model {OnlineModelName} ({onlineID}) was not found in {SosyncSystem.FSOnline.Value} while matching");
            }

            return studioID;
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<dboPersonEmail>())
            {
                var studioModel = db.Read(new { PersonEmailID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", odooPartnerID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var partner_id = 0;

            using (var db = Svc.MdbService.GetDataService<dboPersonEmail>())
            {
                // Get the referenced Studio-IDs
                var personEmail = db.Read(new { PersonEmailID = studioID }).SingleOrDefault();

                partner_id = GetOnlineID<dboPerson>(
                    "dbo.Person",
                    "res.partner",
                    personEmail.PersonID)
                    .Value;
            }

            SimpleTransformToOnline<dboPersonEmail, frstPersonemail>(
                studioID,
                action,
                studioModel => studioModel.PersonEmailID,
                (studio, online) =>
                    {
                        online.Add("email", EmailHelper.MergeEmail(studio.EmailVor, studio.EmailNach));
                        // last_email_update // How to deal with this on MSSQL?
                        online.Add("partner_id", partner_id);
                        online.Add("gueltig_von", studio.GültigVon.Date);
                        online.Add("gueltig_bis", studio.GültigBis.Date);
                        // state -- is only set by FSOnline
                        // main_address -- is only set by FSOnline

                        online.Add("bestaetigt_typ", (object)Svc.TypeService
                            .GetTypeValue(studio.BestaetigungsTypID) ?? false);

                        online.Add("bestaetigt_am_um", DateTimeHelper.ToUtc(studio.BestaetigtAmUm));
                        online.Add("bestaetigt_herkunft", studio.BestaetigungsHerkunft);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32((string)((List<object>)odooModel["partner_id"])[0])
                .Value;

            // Get the corresponding Studio-IDs
            var PersonID = GetStudioID<dboPersonEmail>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            SimpleTransformToStudio<frstPersonemail, dboPersonEmail>(
                onlineID,
                action,
                studioModel => studioModel.PersonEmailID,
                (online, studio) =>
                    {
                        EmailHelper.SplitEmail(online.email, out var mailVor, out var mailNach);

                        studio.PersonID = PersonID;
                        studio.EmailVor = mailVor;
                        studio.EmailNach = mailNach;
                        studio.GültigVon = online.gueltig_von.Date;
                        studio.GültigBis = online.gueltig_bis.Date;
                        studio.HauptAdresse = online.main_address;
                        studio.State = online.state;

                        studio.BestaetigungsTypID = Svc.TypeService
                            .GetTypeID("PersonEmail_BestaetigungsTypID", online.bestaetigt_typ);

                        studio.BestaetigtAmUm = DateTimeHelper.ToLocal(online.bestaetigt_am_um);
                        studio.BestaetigungsHerkunft = online.bestaetigt_herkunft;
                    });
        }
    }
}
