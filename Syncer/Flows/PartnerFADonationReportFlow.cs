using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Odoo;
using Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Data;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.AktionSpendenmeldungBPK")]
    [OnlineModel(Name = "res.partner.fa_donation_report")]
    class PartnerFADonationReportFlow : SyncFlow
    {

        #region Members
        private ILogger<PartnerFlow> _log;
        #endregion

        #region Constructors
        public PartnerFADonationReportFlow(IServiceProvider svc)
            : base(svc)
        {
            _log = (ILogger<PartnerFlow>)svc.GetService(typeof(ILogger<PartnerFlow>));
        }
        #endregion

        #region Methods

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {

            var info = GetDefaultOnlineModelInfo(onlineID, "res.partner.fa_donation_report");

            if(!info.ForeignID.HasValue)
            {

                using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
                {
                    var foundStudioID = db.ExecuteQuery<int?>(
                        $"select AktionsID from dbo.AktionSpendenmeldungBPK where sosync_fso_id = @fso_id",
                        new { fso_id = onlineID })
                        .SingleOrDefault();

                    if (foundStudioID.HasValue)
                        info.ForeignID = foundStudioID;

                }

            }

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

            if(meldung != null)
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

            var meldung = OdooService.Client.GetDictionary("res.partner.fa_donation_report", onlineID, new string[] { "bpk_company_id", "partner_id", "sub_bpk_id" });

            var bpk_company_id = OdooConvert.ToInt32((string)((List<object>)meldung["bpk_company_id"])[0]);
            var partner_id = OdooConvert.ToInt32((string)((List<object>)meldung["partner_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", bpk_company_id.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partner_id.Value);

            if (meldung.ContainsKey("sub_bpk_id"))
            {
                var sub_bpk_id = OdooConvert.ToInt32((string)((List<object>)meldung["sub_bpk_id"])[0]);
                if (sub_bpk_id.HasValue)
                    RequestChildJob(SosyncSystem.FSOnline, "res.partner.bpk", sub_bpk_id.Value);
            }

            

        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {

            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db2 = MdbService.GetDataService<dboAktion>())
            //using (var db3 = MdbService.GetDataService<dboPersonBPK()>)
            {
                var meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
                var aktion = db2.Read(new { AktionsID = studioID }).FirstOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", meldung.xBPKAccountID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", aktion.PersonID);

                if (meldung.SubmissionPersonBPKID != null)
                {
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.PersonBPK", meldung.SubmissionPersonBPKID.Value);
                }

            }

        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {

            dboAktionSpendenmeldungBPK meldung = null;
            dboAktion aktion = null;
            dboPerson person = null;
            dboxBPKAccount bpkAccount = null;
            dboPersonBPK bpk = null;

            using (var db = MdbService.GetDataService<dboAktionSpendenmeldungBPK>())
            using (var db2 = MdbService.GetDataService<dboAktion>())
            using (var db3 = MdbService.GetDataService<dboPerson>())
            using (var db4 = MdbService.GetDataService<dboxBPKAccount>())
            using (var db5 = MdbService.GetDataService<dboPersonBPK>())
            {
                meldung = db.Read(new { AktionsID = studioID }).FirstOrDefault();
                aktion = db2.Read(new { AktionsID = studioID }).FirstOrDefault();
                person = db3.Read(new { PersonID = aktion.PersonID }).FirstOrDefault();
                bpkAccount = db4.Read(new { xBPKAccountID = meldung.xBPKAccountID }).FirstOrDefault();

                if (meldung.SubmissionPersonBPKID.HasValue)
                {
                    bpk = db5.Read(new { PersonBPKID = meldung.SubmissionPersonBPKID.Value }).FirstOrDefault();
                }


            }

            var data = new Dictionary<string, object>()
            {
                {"partner_id", person.sosync_fso_id},
                {"bpk_company_id", bpkAccount.sosync_fso_id},
                {"anlage_am_um", meldung.AnlageAmUm },
                {"ze_datum_von", meldung.ZEDatumVon },
                {"ze_datum_bis", meldung.ZEDatumBis },
                {"meldungs_jahr", meldung.MeldungsJahr },
                {"betrag", meldung.Betrag },
                {"sub_datetime", meldung.SubmissionDate },
                {"sub_url", meldung.SubmissionUrl },
                {"sub_typ", meldung.SubmissionType },
                {"sub_data", meldung.SubmissionData },
                {"sub_response", meldung.SubmissionResponse },
                {"sub_request_time", meldung.SubmissionRequestTime },
                {"sub_log", meldung.SubmissionLog },
                {"sub_bpk_company_name", meldung.SubmissionBPKCompanyName },
                {"sub_bpk_company_stammzahl", meldung.SubmissionBPKCompanyStammzahl },
                {"sub_bpk_private", meldung.SubmissionBPKPrivate },
                {"sub_bpk_public", meldung.SubmissionBPKPublic },
                {"sub_bpk_firstname", meldung.SubmissionBPKFirstname },
                {"sub_bpk_lastname", meldung.SubmissionBPKLastname },
                {"sub_bpk_birthdate", meldung.SubmissionBPKBirthdate },
                {"sub_bpk_zip", meldung.SubmissionBPKZip },
                {"error_code", meldung.ErrorCode },
                {"error_text", meldung.ErrorInformation },
                {"state", meldung.Status }
            };

            if (meldung.SubmissionPersonBPKID.HasValue)
            {
                data.Add("sub_bpk_id", bpk.sosync_fso_id);
            }

            if(action == TransformType.CreateNew)
            {
                data.Add("sosync_fs_id", meldung.AktionsID);

                int onlineMeldungID = 0;

                try
                {
                    onlineMeldungID = OdooService.Client.CreateModel("ddd", data, false);
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

                OdooService.Client.GetModel<resPartnerFADonationReport>("res.partner.fa_donation_report", onlineID);
                UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

                try
                {

                    OdooService.Client.UpdateModel("res.partner.fa_donation_report", data, onlineID, false);
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

            var source = OdooService.Client.GetModel<resPartnerFADonationReport>("res.partner.fa_donation_report", onlineID);

            var source_sosync_write_date = (source.sosync_write_date ?? source.write_date).Value;

            dboAktionSpendenmeldungBPK dest = null;
            dboAktion dest2 = null;

            if(action == TransformType.CreateNew)
            {
                dest = new dboAktionSpendenmeldungBPK();
                dest2 = CreateAktionSpendenmeldungBPKAktion();

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

            CopyFADonationReportToAktionSpendenmeldungBPK(source, dest, dest2);

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

        private dboAktion CreateAktionSpendenmeldungBPKAktion()
        {
            var res = new dboAktion();
            res.AktionsdetailtypID = 2300;
            res.AktionstypID = 2005746; //Aktion_AktionstypID.AktionSpendemeldungBPK
            res.Durchführungstag = DateTime.Today.Date;
            res.Durchführungszeit = DateTime.Today.TimeOfDay;
            res.zMarketingID = 0; //TODO: match better zMarketingID!
            res.zThemaID = 0;
            res.VertragID = 0;
            res.IDHierarchie = 0;

            return res;
        }

        private void CopyFADonationReportToAktionSpendenmeldungBPK(resPartnerFADonationReport source, dboAktionSpendenmeldungBPK dest, dboAktion dest2)
        {

            if (!dest.AnlageAmUm.HasValue)
            {
                dest.AnlageAmUm = source.anlage_am_um;
            }

            dest.ZEDatumVon = source.ze_datum_von;
            dest.ZEDatumBis = source.ze_datum_bis;
            dest.MeldungsJahr = source.meldungs_jahr.Value;
            dest.Betrag = source.betrag.Value;
            dest.SubmissionDate = source.sub_datetime;
            dest.SubmissionUrl = source.sub_url;
            dest.SubmissionType = source.sub_typ;
            dest.SubmissionData = source.sub_data;
            dest.SubmissionResponse = source.sub_response;
            dest.SubmissionRequestTime = source.sub_request_time;
            dest.SubmissionLog = source.sub_log;
            dest.SubmissionBPKCompanyName = source.sub_bpk_company_name;
            dest.SubmissionBPKCompanyStammzahl = source.sub_bpk_company_stammzahl;
            dest.SubmissionBPKPrivate = source.sub_bpk_private;
            dest.SubmissionBPKPublic = source.sub_bpk_public;
            dest.SubmissionBPKFirstname = source.sub_bpk_firstname;
            dest.SubmissionBPKLastname = source.sub_bpk_lastname;
            dest.SubmissionBPKBirthdate = source.sub_bpk_birthdate;
            dest.SubmissionBPKZip = source.sub_bpk_zip;
            dest.ErrorCode = source.error_code;
            dest.ErrorInformation = source.error_text;

            dest.Status = source.state;

            

            using (var db = MdbService.GetDataService<dboPerson>())
            using (var db2 = MdbService.GetDataService<dboxBPKAccount>())
            {
                var pers = db.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)source.partner_id[0]) }).FirstOrDefault();

                dest2.PersonID = pers.PersonID;

                var bpkAcc = db2.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)source.bpk_company_id[0]) }).FirstOrDefault();

                dest.xBPKAccountID = bpkAcc.xBPKAccountID;

            }

            if (OdooConvert.ToInt32((string)source.sub_bpk_id[0]).HasValue)
            {
                using (var db = MdbService.GetDataService<dboPersonBPK>())
                {
                    var bpk = db.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)source.sub_bpk_id[0]) }).FirstOrDefault();

                    if (bpk != null)
                        dest.SubmissionPersonBPKID = bpk.PersonBPKID;

                }
            }

        }

        #endregion
    }
}
