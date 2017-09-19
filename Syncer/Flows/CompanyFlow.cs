using Syncer.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Models;
using WebSosync.Data.Models;
using System.Linq;
using Syncer.Enumerations;
using Odoo;
using Odoo.Models;
using dadi_data.Models;
using Syncer.Exceptions;
using WebSosync.Data;
using Microsoft.Extensions.Logging;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKAccount")]
    [OnlineModel(Name = "res.company")]
    public class CompanyFlow : SyncFlow
    {
        #region Members
        private ILogger<CompanyFlow> _log;
        #endregion

        #region Constructors
        public CompanyFlow(IServiceProvider svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs required
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs required
        }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "res.company");

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId("dbo.xBPKAccount", "xBPKAccountID", onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // A company in FS is just an entry in the xBPKAccount table.
            // So get the write date from that table
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                var acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();
                if (acc != null)
                {
                    if (!acc.sosync_fso_id.HasValue)
                        acc.sosync_fso_id = GetFsoIdByFsId("res.company", acc.xBPKAccountID);

                    return new ModelInfo(studioID, acc.sosync_fso_id, acc.sosync_write_date, acc.write_date);
                }
            }

            return null;
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboxBPKAccount acc = null;
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();

                if (!acc.sosync_fso_id.HasValue)
                    acc.sosync_fso_id = GetFsoIdByFsId("res.company", acc.xBPKAccountID);

                UpdateSyncSourceData(Serializer.ToXML(acc));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>()
                {
                    { "name", acc.Name },
                    { "sosync_write_date", (acc.sosync_write_date ?? acc.write_date.ToUniversalTime()) }
                };

                if (action == TransformType.CreateNew)
                {
                    data.Add("sosync_fs_id", acc.xBPKAccountID);
                    int odooCompanyId = 0;
                    try
                    {
                        var userDic = OdooService.Client.GetDictionary("res.users", OdooService.Client.UserID, new string[] { "company_id" });
                        odooCompanyId = OdooService.Client.CreateModel("res.company", data, false);

                        acc.sosync_fso_id = odooCompanyId;
                        db.Update(acc);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooCompanyId);
                    }
                }
                else
                {
                    var company = OdooService.Client.GetModel<resCompany>("res.company", acc.sosync_fso_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                    try
                    {
                        OdooService.Client.UpdateModel("res.company", data, acc.sosync_fso_id.Value, false);
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
            var company = OdooService.Client.GetModel<resCompany>("res.company", onlineID);

            if (!IsValidFsID(company.Sosync_FS_ID))
                company.Sosync_FS_ID = GetFsIdByFsoId("dbo.xBPKAccount", "xBPKAccountID", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);
            
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                if (action == TransformType.CreateNew)
                {
                    var entry = new dboxBPKAccount()
                    {
                        Name = company.Name,
                        sosync_write_date = (company.Sosync_Write_Date ?? company.Write_Date).Value,
                        sosync_fso_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    var xBPKAccountID = 0;
                    try
                    {
                        db.Create(entry);
                        xBPKAccountID = entry.xBPKAccountID;
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, xBPKAccountID);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), xBPKAccountID);
                        throw;
                    }

                    OdooService.Client.UpdateModel(
                        "res.company",
                        new { sosync_fs_id = entry.xBPKAccountID },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = company.Sosync_FS_ID;
                    var acc = db.Read(new { xBPKAccountID = sosync_fs_id }).SingleOrDefault();

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(acc));

                    acc.Name = company.Name;
                    acc.sosync_write_date = company.Sosync_Write_Date.Value;
                    acc.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(acc));

                    try
                    {
                        db.Update(acc);
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
        #endregion
    }
}
