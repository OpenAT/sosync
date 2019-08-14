using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using dadi_data.Models;
using System.Linq;
using WebSosync.Data.Models;
using WebSosync.Data;
using WebSosync.Common;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using Microsoft.Extensions.Logging;
using Syncer.Services;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKMeldespanne")]
    [OnlineModel(Name = "account.fiscalyear")]
    public class FiscalYearFlow : ReplicateSyncFlow
    {
        #region Constructors
        public FiscalYearFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxBPKMeldespanne>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var fiscal = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "company_id" });
            var companyID = OdooConvert.ToInt32((string)((List<object>)fiscal["company_id"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
            {
                var fiscal = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", fiscal.xBPKAccountID);
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            var companyID = 0;
            dboxBPKMeldespanne spanne = null;
            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
            {
                spanne = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();

                if (!spanne.sosync_fso_id.HasValue)
                    spanne.sosync_fso_id = GetOnlineIDFromOdooViaStudioID(OnlineModelName, spanne.xBPKMeldespanneID);

                companyID = GetOnlineIDFromOdooViaStudioID("res.company", spanne.xBPKAccountID).Value;
            }

            SimpleTransformToOnline<dboxBPKMeldespanne, accountFiscalYear>(
                studioID,
                action,
                studioModel => studioModel.xBPKMeldespanneID,
                (studio, online) =>
                    {
                        online.Add("company_id", companyID);
                        online.Add("name", studio.Bezeichnung);
                        online.Add("code", studio.Kurzbezeichnung);
                        online.Add("date_start", studio.FiskaljahrVon); // No UTC conversion, because this is a date field in Odoo
                        online.Add("date_stop", studio.FiskaljahrBis);  // No UTC conversion, because this is a date field in Odoo
                        online.Add("ze_datum_von", DateTimeHelper.ToUtc(studio.ZE_Datum_Von));
                        online.Add("ze_datum_bis", DateTimeHelper.ToUtc(studio.ZE_Datum_Bis));
                        online.Add("meldezeitraum_start", DateTimeHelper.ToUtc(studio.MeldespanneVon));
                        online.Add("meldezeitraum_end", DateTimeHelper.ToUtc(studio.MeldespanneBis));
                        online.Add("drg_interval_number", studio.ErstellungIntervall);
                        online.Add("drg_interval_type", studio.ErstellungIntervallEinheit);
                        online.Add("drg_next_run", DateTimeHelper.ToUtc(studio.NächsterLauf));
                        online.Add("drg_last", DateTimeHelper.ToUtc(studio.LetzterLauf));
                        online.Add("drg_last_count", studio.LetzterLaufAnzahl);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var fiscal = OdooService.Client.GetModel<accountFiscalYear>(OnlineModelName, onlineID);

            if (!IsValidFsID(fiscal.Sosync_FS_ID))
                fiscal.Sosync_FS_ID = GetStudioIDFromMssqlViaOnlineID(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

            var xBPKAccountID = GetStudioIDFromMssqlViaOnlineID("dbo.xBPKAccount", "xBPKAccountID", Convert.ToInt32(fiscal.CompanyID[0]));

            SimpleTransformToStudio<accountFiscalYear, dboxBPKMeldespanne>(
                onlineID,
                action,
                studioModel => studioModel.xBPKMeldespanneID,
                (online, studio) =>
                    {
                        studio.xBPKAccountID = xBPKAccountID.Value;

                        studio.Bezeichnung = online.Name;
                        studio.Kurzbezeichnung = online.Code;
                        studio.FiskaljahrVon = online.DateStart; // No UTC conversion, field is a 'date' in Odoo
                        studio.FiskaljahrBis = online.DateStop;  // No UTC conversion, field is a 'date' in Odoo

                        studio.ZE_Datum_Von = DateTimeHelper.ToLocal(online.ZeDatumVon);
                        studio.ZE_Datum_Bis = DateTimeHelper.ToLocal(online.ZeDatumBis);
                        studio.MeldespanneVon = DateTimeHelper.ToLocal(online.MeldezeitraumStart);
                        studio.MeldespanneBis = DateTimeHelper.ToLocal(online.MeldezeitraumEnd);

                        var start = DateTimeHelper.ToLocal(online.ZeDatumVon);

                        studio.Meldungsjahr = string.IsNullOrEmpty(online.Meldungs_Jahr)
                            ? (int?)null
                            : int.Parse(online.Meldungs_Jahr);

                        studio.ErstellungIntervall = online.DrgIntervalNumber;
                        studio.ErstellungIntervallEinheit = online.DrgIntervalType;
                        studio.NächsterLauf = DateTimeHelper.ToLocal(online.DrgNextRun);
                        studio.LetzterLauf = DateTimeHelper.ToLocal(online.DrgLast);
                        studio.LetzterLaufAnzahl = online.DrgLastCount;

                        if (action == TransformType.CreateNew)
                            studio.AnlageAmUm = DateTimeHelper.ToLocal(online.Create_Date);
                    });
        }
        #endregion
    }
}
