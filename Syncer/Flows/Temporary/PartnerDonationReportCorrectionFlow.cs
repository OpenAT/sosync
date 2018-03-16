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

            OdooService.Client.UpdateModel(
                OnlineModelName,
                new { donation_deduction_optout_web = optOut, donation_deduction_disabled = deactivate },
                fsoId.Value,
                false);

            LogMilliseconds("Transformation: Odoo update", OdooService.Client.LastRpcTime);
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            throw new NotSupportedException("Correction only possible from [fs] to [fso].");
        }
        #endregion
    }
}
