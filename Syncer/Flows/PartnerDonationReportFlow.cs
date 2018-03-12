using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Odoo;
using Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.AktionSpendenmeldungBPK")]
    [OnlineModel(Name = "res.partner.donation_report")]
    class PartnerDonationReportFlow : ReplicateSyncFlow
    {

        #region Members
        private ILogger<PartnerFlow> _log;
        #endregion

        #region Constructors
        public PartnerDonationReportFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
            _log = (ILogger<PartnerFlow>)svc.GetService(typeof(ILogger<PartnerFlow>));
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "res.partner.donation_report");

            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId("dbo.AktionSpendenmeldungBPK", "AktionSpendenmeldungBPK", onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            ModelInfo result = null;
            dboAktionSpendenmeldungBPK meldung = null;

            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            {
                meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
            }

            if (meldung != null)
            {
                return new ModelInfo(
                    studioID,
                    meldung.sosync_fso_id,
                    meldung.sosync_write_date,
                    meldung.write_date);
            }

            return result;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var meldung = OdooService.Client.GetDictionary("res.partner.donation_report", onlineID, new string[] { "bpk_company_id", "partner_id" });

            var bpk_company_id = OdooConvert.ToInt32((string)((List<object>)meldung["bpk_company_id"])[0]);
            var partner_id = OdooConvert.ToInt32((string)((List<object>)meldung["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", bpk_company_id.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partner_id.Value);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db2 = MdbService.GetDataService<dboAktion>())
            {
                var meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
                var aktion = db2.Read(new { AktionsID = studioID }).FirstOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", meldung.xBPKAccountID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", aktion.PersonID);
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboAktionSpendenmeldungBPK meldung = null;
            dboxBPKAccount bpkAccount = null;
            var partnerID = 0;

            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db4 = MdbService.GetDataService<dboxBPKAccount>())
            using (var db5 = MdbService.GetDataService<dboPersonBPK>())
            {
                meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
                var personID = db.ExecuteQuery<int>("select PersonID from dbo.Aktion where AktionsID = @AktionsID", new { AktionsID = studioID }).Single(); 
                partnerID = db.ExecuteQuery<int>("select sosync_fso_id from dbo.Person where PersonID = @PersonID", new { PersonID = personID }).Single();
                bpkAccount = db4.Read(new { xBPKAccountID = meldung.xBPKAccountID }).FirstOrDefault();
            }

            // Never synchronize test entries
            if ((meldung.SubmissionEnv ?? "").ToUpper() != "P")
                throw new SyncerException($"{StudioModelName} can only be synchronized with submission_env = 'P' for production. Test entries cannot be synchronized.");

            var data = new Dictionary<string, object>()
            {
                { "submission_env", meldung.SubmissionEnv },
                { "partner_id", partnerID },
                { "bpk_company_id", bpkAccount.sosync_fso_id },
                { "anlage_am_um", meldung.AnlageAmUm.Value.ToUniversalTime() },
                { "ze_datum_von", meldung.ZEDatumVon.Value.ToUniversalTime() },
                { "ze_datum_bis", meldung.ZEDatumBis.Value.ToUniversalTime() },
                { "meldungs_jahr", meldung.MeldungsJahr.ToString("0") },
                { "betrag", meldung.Betrag },
                { "cancellation_for_bpk_private", meldung.CancellationForBpkPrivate },
                { "info", meldung.Info },
                { "sosync_write_date", meldung.sosync_write_date }
            };

            if (action == TransformType.CreateNew)
            {
                data.Add("sosync_fs_id", meldung.AktionsID);
                int onlineMeldungID = 0;

                try
                {
                    onlineMeldungID = OdooService.Client.CreateModel(OnlineModelName, data, false);
                }
                finally
                {
                    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, onlineMeldungID);
                }

                using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
                {
                    meldung.sosync_fso_id = onlineMeldungID;
                    meldung.noSyncJobSwitch = true;
                    db.Update(meldung);
                }
            }
            else
            {
                var onlineID = meldung.sosync_fso_id.Value;

                OdooService.Client.GetModel<resPartnerDonationReport>(OnlineModelName, onlineID);
                UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

                try
                {
                    OdooService.Client.UpdateModel(OnlineModelName, data, onlineID, false);
                }
                finally
                {
                    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var source = OdooService.Client.GetModel<resPartnerDonationReport>(OnlineModelName, onlineID);
            var source_sosync_write_date = (source.sosync_write_date ?? source.write_date).Value;

            // Never synchronize test entries
            if ((source.submission_env ?? "").ToUpper() != "P")
                throw new SyncerException($"{OnlineModelName} can only be synchronized with submission_env = 'P' for production. Test entries cannot be synchronized.");

            dboAktionSpendenmeldungBPK dest = null;
            dboAktion dest2 = null;

            if (action == TransformType.CreateNew)
            {
                //dest = new dboAktionSpendenmeldungBPK();
                //dest2 = CreateAktionSpendenmeldungBPKAktion();
                throw new SyncerException($"{OnlineModelName} can only be updated, not created.");
            }
            else
            {
                using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
                using (var db2 = MdbService.GetDataService<dboAktion>())
                {
                    dest = db.Read(new { sosync_fso_id = onlineID }).FirstOrDefault();
                    dest2 = db2.Read(new { AktionsID = dest.AktionsID }).FirstOrDefault();
                }
            }

            CopyDonationReportToAktionSpendenmeldungBPK(source, dest, dest2);

            dest.sosync_write_date = source_sosync_write_date;
            dest.noSyncJobSwitch = true;

            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db2 = MdbService.GetDataService<dboAktion>())
            {
                if (action == TransformType.CreateNew)
                {
                    db2.Create(dest2);
                    dest.AktionsID = dest2.AktionsID;
                    db.Create(dest);
                }
                else
                {
                    db.Update(dest);
                    db2.Update(dest2);
                }
            }
        }

        //private dboAktion CreateAktionSpendenmeldungBPKAktion()
        //{
        //    var res = new dboAktion();
        //    res.AktionsdetailtypID = 2300;
        //    res.AktionstypID = 2005746; //Aktion_AktionstypID.AktionSpendemeldungBPK
        //    res.Durchführungstag = DateTime.Today.Date;
        //    res.Durchführungszeit = DateTime.Today.TimeOfDay;
        //    res.zMarketingID = 0; //TODO: match better zMarketingID!
        //    res.zThemaID = 0;
        //    res.VertragID = 0;
        //    res.IDHierarchie = 0;

        //    return res;
        //}

        private void CopyDonationReportToAktionSpendenmeldungBPK(resPartnerDonationReport source, dboAktionSpendenmeldungBPK dest, dboAktion dest2)
        {
            if (!dest.AnlageAmUm.HasValue)
                dest.AnlageAmUm = source.anlage_am_um;

            dest.Status = source.state;
            dest.Info = source.info;
            dest.SubmissionEnv = source.submission_env;
            dest2.PersonID = GetFsIdByFsoId("dbo.Person", "PersonID", Convert.ToInt32(source.partner_id[0])).Value;
            dest.xBPKAccountID = GetFsIdByFsoId("dbo.xBPKAccount", "xBPKAccountID", Convert.ToInt32(source.bpk_company_id[0])).Value;
            dest.SubmissionCompanyName = (string)source.bpk_company_id[1];
            dest.AnlageAmUm = source.anlage_am_um.Value.ToLocalTime();
            dest.ZEDatumVon = source.ze_datum_von.Value.ToLocalTime();
            dest.ZEDatumBis = source.ze_datum_bis.Value.ToLocalTime();
            dest.MeldungsJahr =  source.meldungs_jahr.Value;
            dest.Betrag = source.betrag.Value;
            dest.CancellationForBpkPrivate = source.cancellation_for_bpk_private;
            dest.SubmissionType = source.submission_type;
            dest.SubmissionRefnr = source.submission_refnr;
            dest.SubmissionFirstname = source.submission_firstname;
            dest.SubmissionLastname = source.submission_lastname;
            dest.SubmissionBirthdateWeb = source.submission_birthdate_web;
            dest.SubmissionZip = source.submission_zip;
            dest.SubmissionIdDate = source.submission_id_datetime;

            if (!string.IsNullOrEmpty(source.submission_bpk_request_id))
                dest.SubmissionBPKRequestID = Convert.ToInt32(source.submission_bpk_request_id); // 1:1 speichern

            dest.SubmissionBPKPublic = source.submission_bpk_public;
            dest.SubmissionBPKPrivate = source.submission_bpk_private;
            dest.ResponseContent = source.response_content;
            dest.ResponseErrorCode = source.response_error_code;
            dest.ResponseErrorDetail = source.response_error_detail;
            dest.ErrorType = source.error_type;
            dest.ErrorCode = source.error_code;
            dest.ErrorDetail = source.error_detail;

            dest.ResponseErrorOrigRefnr = source.response_error_orig_refnr;

            // -- source.report_erstmeldung_id;
            // -- source.report_follow_up_ids;
            // -- source.skipped_by_id;
            // -- source.skipped;
            
            using (var db = MdbService.GetDataService<dboPerson>())
            using (var db2 = MdbService.GetDataService<dboxBPKAccount>())
            {
                var pers = db.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)source.partner_id[0]) }).FirstOrDefault();
                dest2.PersonID = pers.PersonID;

                var bpkAcc = db2.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)source.bpk_company_id[0]) }).FirstOrDefault();
                dest.xBPKAccountID = bpkAcc.xBPKAccountID;
            }
        }

        #endregion
    }
}
