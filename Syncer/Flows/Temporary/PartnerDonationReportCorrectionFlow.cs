using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using WebSosync.Data.Models;
using Syncer.Attributes;
using dadi_data.Models;
using System.Linq;
using Syncer.Exceptions;
using dadi_data.Interfaces;
using WebSosync.Data;

namespace Syncer.Flows.Temporary
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    public class PartnerDonationReportCorrectionFlow : TempSyncFlow
    {
        #region Constructors
        public PartnerDonationReportCorrectionFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }
        #endregion

        #region Methods
        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
        }

        private bool IsInGroup(IStudioGroup group, DateTime when)
        {
            if (group != null && group.GültigVon <= when && group.GültigBis >= when)
                return true;

            return false;
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
#warning TODO: This should be set from the source systems when the sync job is submitted via REST api
            if (String.IsNullOrEmpty(Job.Job_Source_Type_Info))
                Job.Job_Source_Type_Info = "donation_deduction_fields_repair";

            int? fsoId = null;
            var optOut = false;
            var deactivate = false;
            var newsletter = false;
            var receipt = false;

            using (var db = MdbService.GetDataService<dboPersonGruppe>())
            using (var db2 = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                fsoId = db.ExecuteQuery<int?>(
                    "select sosync_fso_id from dbo.Person where PersonID = @PersonID",
                    new { PersonID = studioID })
                    .SingleOrDefault();
                LogMilliseconds("Transformation: Get sosync_fso_id", db.LastQueryExecutionTimeMS);

                if (fsoId == null)
                    throw new SyncerException($"Could not find sosync_fso_id for {StudioModelName} ({studioID}).");

                var optOutGroup = db.Read(new { PersonID = studioID, zGruppeDetailID = 110493 }).FirstOrDefault();
                LogMilliseconds("Transformation: Get OptOut group", db.LastQueryExecutionTimeMS);

                var deactivateGroup = db.Read(new { PersonID = studioID, zGruppeDetailID = 128782 }).FirstOrDefault();
                LogMilliseconds("Transformation: Get Deactivate group", db.LastQueryExecutionTimeMS);

                var receiptGroup = db.Read(new { PersonID = studioID, zGruppeDetailID = 20168 }).FirstOrDefault();
                LogMilliseconds("Transformation: Get Receipt group", db.LastQueryExecutionTimeMS);

                var emailID = db2.ExecuteQuery<int?>(
                    "select top 1 PersonEmailID from dbo.PersonEmail where PersonID = @PersonID and cast(getdate() as date) between GültigVon and GültigBis order by isnull(GültigMonatArray, '111111111111') desc, PersonEmailID desc",
                    new { PersonID = studioID })
                    .SingleOrDefault();
                LogMilliseconds("Transformation: Get PersonEmailID", db.LastQueryExecutionTimeMS);

                var newsletterGroup = db2.Read(new { PersonEmailID = emailID, zGruppeDetailID = 30104 }).FirstOrDefault();
                LogMilliseconds("Transformation: Get PersonEmailGruppe", db.LastQueryExecutionTimeMS);

                optOut = IsInGroup(optOutGroup, DateTime.Today);
                deactivate = IsInGroup(deactivateGroup, DateTime.Today);
                newsletter = IsInGroup(newsletterGroup, DateTime.Today);
                receipt = IsInGroup(receiptGroup, DateTime.Today);
            }

            UpdateSyncSourceData(Serializer.ToXML(new DonationCorrectionFields(optOut, deactivate, receipt, newsletter)));

            var partnerDict = OdooService.Client.GetDictionary(
                OnlineModelName,
                fsoId.Value,
                new[] { "donation_deduction_optout_web", "donation_deduction_disabled", "donation_receipt_web", "newsletter_web" });

            LogMilliseconds("Transformation: Reading current res.partner (4 fields)", OdooService.Client.LastRpcTime);

            if (partnerDict.Count == 1)
                throw new SyncerException($"[{SosyncSystem.FundraisingStudio}] {StudioModelName} ({studioID}) did not exist in [{SosyncSystem.FSOnline}] {OnlineModelName} ({fsoId.Value})");

            var changes =
                Convert.ToString(partnerDict["donation_deduction_optout_web"]) != BoolToString(optOut)
                || Convert.ToString(partnerDict["donation_deduction_disabled"]) != BoolToString(deactivate)
                || Convert.ToString(partnerDict["donation_receipt_web"]) != BoolToString(receipt)
                || Convert.ToString(partnerDict["newsletter_web"]) != BoolToString(newsletter);

            if (changes)
            {
                UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(
                    new DonationCorrectionFields(
                        Convert.ToInt32(partnerDict["donation_deduction_optout_web"]) != 0,
                        Convert.ToInt32(partnerDict["donation_deduction_disabled"]) != 0,
                        Convert.ToInt32(partnerDict["donation_receipt_web"]) != 0,
                        Convert.ToInt32(partnerDict["newsletter_web"]) != 0)));

                UpdateSyncTargetRequest(Serializer.ToXML(new DonationCorrectionFields(optOut, deactivate, receipt, newsletter)));

                try
                {
                    OdooService.Client.UpdateModel(
                        OnlineModelName,
                        new { donation_deduction_optout_web = optOut, donation_deduction_disabled = deactivate },
                        fsoId.Value,
                        false);

                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);

                    LogMilliseconds("Transformation: Odoo update", OdooService.Client.LastRpcTime);
                }
                catch (Exception ex)
                {
                    UpdateSyncTargetAnswer(ex.ToString(), null);
                    throw;
                }
            }
            else
            {
                LogMilliseconds("Transformation: Odoo no changes", 0);
            }
        }

        private string BoolToString(bool val)
        {
            return val ? "1" : "0";
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException("Correction only possible from [fs] to [fso].");
        }
        #endregion
    }

    public class DonationCorrectionFields
    {
        public DonationCorrectionFields()
        { }

        public DonationCorrectionFields(bool optOut, bool disabled, bool receipt, bool news)
        {
            donation_deduction_optout_web = optOut;
            donation_deduction_disabled = disabled;
            donation_receipt_web = receipt;
            newsletter_web = news;
        }

        public bool donation_deduction_optout_web { get; set; }
        public bool donation_deduction_disabled { get; set; }
        public bool donation_receipt_web { get; set; }
        public bool newsletter_web { get; set; }
    }
}