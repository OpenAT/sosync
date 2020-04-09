using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.AktionSpendenmeldungBPK")]
    [OnlineModel(Name = "res.partner.donation_report")]
    [ModelPriority(3000)]
    class PartnerDonationReportFlow : ReplicateSyncFlow
    {
        #region Constructors
        public PartnerDonationReportFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboAktionSpendenmeldungBPK>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var meldung = Svc.OdooService.Client.GetDictionary("res.partner.donation_report", onlineID, new string[] { "bpk_company_id", "partner_id" });

            var bpk_company_id = OdooConvert.ToInt32((string)((List<object>)meldung["bpk_company_id"])[0]);
            var partner_id = OdooConvert.ToInt32((string)((List<object>)meldung["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", bpk_company_id.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partner_id.Value);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db2 = Svc.MdbService.GetDataService<dboAktion>())
            {
                var meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
                var aktion = db2.Read(new { AktionsID = studioID }).FirstOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", meldung.xBPKAccountID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", aktion.PersonID);
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboAktion aktion = null;
            using (var db = Svc.MdbService.GetDataService<dboAktion>())
            {
                aktion = db.Read(new { AktionsID = studioID })
                    .SingleOrDefault();
            }

            SimpleTransformToOnline<dboAktionSpendenmeldungBPK, resPartnerDonationReport>(
                studioID,
                action,
                x => x.AktionsID,
                (studio, online) =>
                {
                    var partnerID = GetOnlineID<dboPerson>(
                        "dbo.Person",
                        "res.partner",
                        aktion.PersonID);

                    var companyID = GetOnlineID<dboPerson>(
                        "dbo.xBPKAccount",
                        "res.company",
                        studio.xBPKAccountID);

                    online.Add("submission_env", studio.SubmissionEnv);
                    online.Add("partner_id", partnerID.Value);
                    online.Add("bpk_company_id", companyID.Value);
                    online.Add("anlage_am_um", studio.AnlageAmUm.Value.ToUniversalTime());
                    online.Add("ze_datum_von", studio.ZEDatumVon.Value.ToUniversalTime());
                    online.Add("ze_datum_bis", studio.ZEDatumBis.Value.ToUniversalTime());
                    online.Add("meldungs_jahr", studio.MeldungsJahr.ToString("0"));
                    online.Add("betrag", studio.Betrag);
                    online.Add("cancellation_for_bpk_private", studio.CancellationForBpkPrivate);
                    online.Add("info", studio.Info);
                    online.Add("force_submission", studio.ForceSubmission);
                    online.Add("state", studio.Status);
                    online.Add("imported", studio.Imported);
                    online.Add("submission_bpk_private", studio.SubmissionBPKPrivate);
                    online.Add("submission_refnr", studio.SubmissionRefnr);
                    online.Add("create_reason", studio.create_reason);

                    // Sync submission date only for imported donation reports
                    if (studio.Imported == true)
                    {
                        online.Add("submission_id_datetime", studio.SubmissionIdDate);
                        online.Add("submission_type", studio.SubmissionType);
                    }

                    if (!string.IsNullOrEmpty(studio.donor_instruction))
                    {
                        online.Add("donor_instruction", studio.donor_instruction);
                        online.Add("donor_instruction_info", studio.donor_instruction_info);
                    }
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
            var PersonID = GetStudioID<dboPerson>(
                "res.partner",
                "dbo.Person",
                odooPartnerID)
                .Value;

            var spendenmeldungAktion = GetSpendenmeldungAktionViaOnlineID(onlineID, action);
            spendenmeldungAktion.PersonID = PersonID;


            SimpleTransformToStudio<resPartnerDonationReport, dboAktionSpendenmeldungBPK>(
                onlineID,
                action,
                x => x.AktionsID,
                (online, studio) =>
                {
                    if (!studio.AnlageAmUm.HasValue)
                        studio.AnlageAmUm = online.anlage_am_um;

                    studio.Status = online.state;
                    studio.Info = online.info;
                    studio.SubmissionEnv = online.submission_env;

                    studio.xBPKAccountID = GetStudioIDFromMssqlViaOnlineID(
                        "dbo.xBPKAccount",
                        "xBPKAccountID",
                        Convert.ToInt32(online.bpk_company_id[0]))
                        .Value;

                    studio.SubmissionCompanyName = (string)online.bpk_company_id[1];
                    studio.AnlageAmUm = online.anlage_am_um.Value.ToLocalTime();
                    studio.ZEDatumVon = online.ze_datum_von.Value.ToLocalTime();
                    studio.ZEDatumBis = online.ze_datum_bis.Value.ToLocalTime();
                    studio.MeldungsJahr = online.meldungs_jahr.Value;
                    studio.Betrag = online.betrag.Value;
                    studio.CancellationForBpkPrivate = online.cancellation_for_bpk_private;
                    studio.SubmissionType = online.submission_type;
                    studio.SubmissionRefnr = online.submission_refnr;
                    studio.SubmissionFirstname = online.submission_firstname;
                    studio.SubmissionLastname = online.submission_lastname;
                    studio.SubmissionBirthdateWeb = online.submission_birthdate_web;
                    studio.SubmissionZip = online.submission_zip;

                    if (online.submission_id_datetime.HasValue)
                        studio.SubmissionIdDate = online.submission_id_datetime.Value.ToLocalTime();
                    else
                        studio.SubmissionIdDate = null;

                    if (!string.IsNullOrEmpty(online.submission_bpk_request_id))
                        studio.SubmissionBPKRequestID = Convert.ToInt32(online.submission_bpk_request_id); // 1:1 speichern

                    studio.SubmissionBPKPublic = online.submission_bpk_public;
                    studio.SubmissionBPKPrivate = online.submission_bpk_private;
                    studio.ResponseContent = online.response_content;
                    studio.ResponseErrorCode = online.response_error_code;
                    studio.ResponseErrorDetail = online.response_error_detail;
                    studio.ErrorType = online.error_type;
                    studio.ErrorCode = online.error_code;
                    studio.ErrorDetail = online.error_detail;
                    studio.ResponseErrorOrigRefnr = online.response_error_orig_refnr;
                    studio.ForceSubmission = online.force_submission;
                    studio.Imported = online.imported;

                    studio.create_reason = online.create_reason;
                    studio.donor_instruction = online.donor_instruction;
                    studio.donor_instruction_info = online.donor_instruction_info;

                    // -- online.report_erstmeldung_id;
                    // -- online.report_follow_up_ids;
                    // -- online.skipped_by_id;
                    // -- online.skipped;
                },
                spendenmeldungAktion,
                (online, aktionsID, asm) => { asm.AktionsID = aktionsID; });
        }

        private dboAktion GetSpendenmeldungAktionViaOnlineID(int onlineID, TransformType action)
        {
            if (action == TransformType.CreateNew)
            {
                throw new SyncerException($"Cannot create Aktion. Only update is supported.");
            }
            else
            {
                using (var db = Svc.MdbService.GetDataService<dboAktion>())
                {
                    return db.ExecuteQuery<dboAktion>(
                        "SELECT a.* FROM dbo.AktionSpendenmeldungBPK at " +
                        "INNER JOIN dbo.Aktion a on at.AktionsID = a.AktionsID " +
                        "WHERE at.sosync_fso_id = @sosync_fso_id",
                        new { sosync_fso_id = onlineID }).SingleOrDefault();
                }
            }
        }
        #endregion
    }
}
