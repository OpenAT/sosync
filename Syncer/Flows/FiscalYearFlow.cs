//using System;
//using System.Collections.Generic;
//using System.Text;
//using Syncer.Enumerations;
//using Syncer.Models;
//using Syncer.Attributes;
//using dadi_data.Models;
//using System.Linq;
//using WebSosync.Data.Models;
//using Odoo;
//using WebSosync.Data;
//using Odoo.Models;

//namespace Syncer.Flows
//{
//    [StudioModel(Name = "dbo.xBPKMeldespanne")]
//    [OnlineModel(Name = "account.fiscalyear")]
//    public class FiscalYearFlow : ReplicateSyncFlow
//    {
//        #region Constructors
//        public FiscalYearFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
//        {
//        }
//        #endregion

//        #region Methods
//        protected override ModelInfo GetOnlineInfo(int onlineID)
//        {
//            var info = GetDefaultOnlineModelInfo(onlineID, "account.fiscalyear");

//            // If there was no foreign ID in fso, try to check the mssql side
//            // for the referenced ID too
//            if (!info.ForeignID.HasValue)
//                info.ForeignID = GetFsIdByFsoId("dbo.xBPKMeldespanne", "xBPKMeldespanneID", onlineID);

//            return info;
//        }

//        protected override ModelInfo GetStudioInfo(int studioID)
//        {
//            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
//            {
//                var fiscal = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();
//                if (fiscal != null)
//                {
//                    if (!fiscal.sosync_fso_id.HasValue)
//                        fiscal.sosync_fso_id = GetFsoIdByFsId("account.fiscalyear", fiscal.xBPKMeldespanneID);

//                    return new ModelInfo(studioID, fiscal.sosync_fso_id, fiscal.sosync_write_date, fiscal.write_date);
//                }
//            }

//            return null;
//        }

//        protected override void SetupOnlineToStudioChildJobs(int onlineID)
//        {
//            var fiscal = OdooService.Client.GetDictionary("account.fiscalyear", onlineID, new string[] { "company_id" });
//            var companyID = OdooConvert.ToInt32((string)((List<object>)fiscal["company_id"])[0]);

//            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value);
//        }

//        protected override void SetupStudioToOnlineChildJobs(int studioID)
//        {
//            // Temporary only, later on synchronization back should not be supported
//            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
//            {
//                var fiscal = db.Read(new { xBPKMeldespanneID = studioID }).SingleOrDefault();
//                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", fiscal.xBPKAccountID);
//            }
//        }

//        protected override void TransformToOnline(int studioID, TransformType action)
//        {
//            // Temporary only, later on synchronization back should not be supported
//            throw new NotImplementedException();
//        }

//        protected override void TransformToStudio(int onlineID, TransformType action)
//        {
//            var fiscal = OdooService.Client.GetModel<accountFiscalYear>(OnlineModelName, onlineID);

//            if (!IsValidFsID(fiscal.Sosync_FS_ID))
//                fiscal.Sosync_FS_ID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

//            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

//            using (var db = MdbService.GetDataService<dboxBPKMeldespanne>())
//            {
//                if (action == TransformType.CreateNew)
//                {
//                    var entry = new dboxBPKMeldespanne();
//                    CopyFiscalToMeldespanne(fiscal, entry, onlineID, true);

//                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

//                    var xBPKMeldespanneID = 0;
//                    try
//                    {
//                        db.Create(entry);
//                        xBPKMeldespanneID = entry.xBPKMeldespanneID;
//                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, xBPKMeldespanneID);
//                    }
//                    catch (Exception ex)
//                    {
//                        UpdateSyncTargetAnswer(ex.ToString(), xBPKMeldespanneID);
//                        throw;
//                    }

//                    OdooService.Client.UpdateModel(
//                        OnlineModelName,
//                        new { sosync_fs_id = entry.xBPKMeldespanneID },
//                        onlineID,
//                        false);
//                }
//                else
//                {
//                    var sosync_fs_id = fiscal.Sosync_FS_ID;
//                    var entry = db.Read(new { xBPKMeldespanneID = sosync_fs_id }).SingleOrDefault();

//                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(entry));
//                    CopyFiscalToMeldespanne(fiscal, entry, onlineID, false);
//                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

//                    try
//                    {
//                        db.Update(entry);
//                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
//                    }
//                    catch (Exception ex)
//                    {
//                        UpdateSyncTargetAnswer(ex.ToString(), null);
//                        throw;
//                    }
//                }
//            }
//        }

//        private void CopyFiscalToMeldespanne(accountFiscalYear fiscal, dboxBPKMeldespanne entry, int onlineID, bool isNew)
//        {
//            entry.Bezeichnung = fiscal.Name;
//            entry.Kurzbezeichnung = fiscal.Code;
//            entry.FiskaljahrVon = fiscal.DateStart;
//            entry.FiskaljahrBis = fiscal.DateStop;

//            entry.ZE_Datum_Von = fiscal.ZeDatumVon;
//            entry.ZE_Datum_Bis = fiscal.ZeDatumBis;
//            entry.MeldespanneVon = fiscal.MeldezeitraumStart;
//            entry.MeldespanneBis = fiscal.MeldezeitraumEnd;

//            entry.ErstellungIntervall = fiscal.DrgIntervalNumber;
//            entry.ErstellungIntervallEinheit = fiscal.DrgIntervalType;
//            entry.LetzterLauf = fiscal.DrgLast;
//            entry.NächsterLauf = fiscal.DrgNextRun;

//            entry.sosync_write_date = (fiscal.Sosync_Write_Date ?? fiscal.Write_Date).Value;
//            entry.noSyncJobSwitch = true;

//            if (isNew)
//            {
//                entry.AnlageAmUm = fiscal.Create_Date;
//                entry.sosync_fso_id = onlineID;
//            }
//        }
//        #endregion
//    }
//}
