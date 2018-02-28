using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using dadi_data.Models;
using System.Linq;
using WebSosync.Data.Models;
using Odoo;
using WebSosync.Data;
using Odoo.Models;
using WebSosync.Common;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKMeldespanne")]
    [OnlineModel(Name = "account.fiscalyear")]
    public class FiscalYearFlow : ReplicateSyncFlow
    {
        #region Constructors
        public FiscalYearFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "account.fiscalyear");

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId("dbo.xBPKMeldespanne", "xBPKMeldespanneID", onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
            {
                var fiscal = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();
                if (fiscal != null)
                {
                    if (!fiscal.sosync_fso_id.HasValue)
                        fiscal.sosync_fso_id = GetFsoIdByFsId("account.fiscalyear", fiscal.xBPKMeldespanneID);

                    return new ModelInfo(studioID, fiscal.sosync_fso_id, fiscal.sosync_write_date, fiscal.write_date);
                }
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var fiscal = OdooService.Client.GetDictionary("account.fiscalyear", onlineID, new string[] { "company_id" });
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
            dboxBPKMeldespanne spanne = null;

            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
            {
                spanne = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();

                if (!spanne.sosync_fso_id.HasValue)
                    spanne.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, spanne.xBPKMeldespanneID);

                var companyID = GetFsoIdByFsId("res.company", spanne.xBPKAccountID);

                UpdateSyncSourceData(Serializer.ToXML(spanne));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>()
                {
                    { "company_id", companyID },
                    { "name", spanne.Bezeichnung },
                    { "code", spanne.Kurzbezeichnung },
                    { "date_start", spanne.FiskaljahrVon }, // No UTC conversion, because this is a date field in Odoo
                    { "date_stop", spanne.FiskaljahrBis },  // No UTC conversion, because this is a date field in Odoo
                    { "ze_datum_von", DateTimeHelper.ToUtc(spanne.ZE_Datum_Von) },
                    { "ze_datum_bis", DateTimeHelper.ToUtc(spanne.ZE_Datum_Bis) },
                    { "meldezeitraum_start", DateTimeHelper.ToUtc(spanne.MeldespanneVon) },
                    { "meldezeitraum_end", DateTimeHelper.ToUtc(spanne.MeldespanneBis) },
                    { "drg_interval_number", spanne.ErstellungIntervall },
                    { "drg_interval_type", spanne.ErstellungIntervallEinheit},
                    { "drg_next_run", DateTimeHelper.ToUtc(spanne.NächsterLauf) },
                    { "drg_last", DateTimeHelper.ToUtc(spanne.LetzterLauf) },
                    { "sosync_write_date", (spanne.sosync_write_date ?? spanne.write_date.ToUniversalTime()) }
                };

                if (action == TransformType.CreateNew)
                {
                    data.Add("sosync_fs_id", spanne.xBPKMeldespanneID);
                    int odooFiscalID = 0;

                    try
                    {
                        odooFiscalID = OdooService.Client.CreateModel(OnlineModelName, data, false);
                        spanne.sosync_fso_id = odooFiscalID;
                        db.Update(spanne);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooFiscalID);
                    }
                }
                else
                {
                    var fiscalYear = OdooService.Client.GetModel<resCompany>(OnlineModelName, spanne.sosync_fso_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                    try
                    {
                        OdooService.Client.UpdateModel(OnlineModelName, data, spanne.sosync_fso_id.Value, false);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
                    }
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var fiscal = OdooService.Client.GetModel<accountFiscalYear>(OnlineModelName, onlineID);

            if (!IsValidFsID(fiscal.Sosync_FS_ID))
                fiscal.Sosync_FS_ID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
            {
                var xBPKAccountID = GetFsIdByFsoId("dbo.xBPKAccount", "xBPKAccountID", Convert.ToInt32(fiscal.CompanyID[0]));

                if (action == TransformType.CreateNew)
                {
                    var entry = new dboxBPKMeldespanne();
                    CopyFiscalToMeldespanne(fiscal, entry, onlineID, xBPKAccountID.Value, true);

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    var xBPKMeldespanneID = 0;
                    try
                    {
                        db.Create(entry);
                        xBPKMeldespanneID = entry.xBPKMeldespanneID;
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, xBPKMeldespanneID);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), xBPKMeldespanneID);
                        throw;
                    }

                    OdooService.Client.UpdateModel(
                        OnlineModelName,
                        new { sosync_fs_id = entry.xBPKMeldespanneID },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = fiscal.Sosync_FS_ID;
                    var entry = db.Read(new { xBPKMeldespanneID = sosync_fs_id }).SingleOrDefault();

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(entry));
                    CopyFiscalToMeldespanne(fiscal, entry, onlineID, xBPKAccountID.Value, false);
                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    try
                    {
                        db.Update(entry);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), null);
                        throw;
                    }
                }
            }
        }

        private void CopyFiscalToMeldespanne(accountFiscalYear fiscal, dboxBPKMeldespanne entry, int onlineID, int xBPKAccountID, bool isNew)
        {
            entry.xBPKAccountID = xBPKAccountID;

            entry.Bezeichnung = fiscal.Name;
            entry.Kurzbezeichnung = fiscal.Code;
            entry.FiskaljahrVon = fiscal.DateStart; // No UTC conversion, field is a 'date' in Odoo
            entry.FiskaljahrBis = fiscal.DateStop;  // No UTC conversion, field is a 'date' in Odoo

            entry.ZE_Datum_Von = DateTimeHelper.ToLocal(fiscal.ZeDatumVon);
            entry.ZE_Datum_Bis = DateTimeHelper.ToLocal(fiscal.ZeDatumBis);
            entry.MeldespanneVon = DateTimeHelper.ToLocal(fiscal.MeldezeitraumStart);
            entry.MeldespanneBis = DateTimeHelper.ToLocal(fiscal.MeldezeitraumEnd);

            var start = DateTimeHelper.ToLocal(fiscal.ZeDatumVon);
            entry.Meldungsjahr = start.HasValue ? start.Value.Year : 0;

            entry.ErstellungIntervall = fiscal.DrgIntervalNumber;
            entry.ErstellungIntervallEinheit = fiscal.DrgIntervalType;
            entry.NächsterLauf = DateTimeHelper.ToLocal(fiscal.DrgNextRun);
            entry.LetzterLauf = DateTimeHelper.ToLocal(fiscal.DrgLast);

            entry.sosync_write_date = (fiscal.Sosync_Write_Date ?? fiscal.Write_Date).Value;
            entry.noSyncJobSwitch = true;

            if (isNew)
            {
                entry.AnlageAmUm = DateTimeHelper.ToLocal(fiscal.Create_Date);
                entry.sosync_fso_id = onlineID;
            }
        }
        #endregion
    }
}
