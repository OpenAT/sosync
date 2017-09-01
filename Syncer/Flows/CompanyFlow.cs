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

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xBPKAccount")]
    [OnlineModel(Name = "res.company")]
    public class CompanyFlow : SyncFlow
    {
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
            return GetDefaultOnlineModelInfo(onlineID, "res.company");
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // A company in FS is just an entry in the xBPKAccount table.
            // So get the write date from that table
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                var acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();
                if (acc != null)
                    return new ModelInfo(studioID, acc.res_company_id, acc.sosync_write_date, acc.write_date);
            }

            return null;
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboxBPKAccount acc = null;
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();

                UpdateSyncSourceData(Serializer.ToXML(acc));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>()
                {
                    { "name", acc.Name },
                    { "sosync_write_date", (acc.sosync_write_date ?? acc.write_date).ToUniversalTime() }
                };

                if (action == TransformType.CreateNew)
                {
                    data.Add("sosync_fs_id", acc.xBPKAccountID);
                    try
                    {
                        var userDic = OdooService.Client.GetDictionary("res.users", OdooService.Client.UserID, new string[] { "company_id" });
                        var parentID = OdooConvert.ToInt32((string)((List<object>)userDic["company_id"])[0]);

                        if (parentID.HasValue)
                            data.Add("parent_id", parentID);

                        int odooCompanyId = OdooService.Client.CreateModel("res.company", data, false);

                        acc.res_company_id = odooCompanyId;
                        db.Update(acc);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw);
                    }
                }
                else
                {
                    var company = OdooService.Client.GetModel<resCompany>("res.company", acc.res_company_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                    try
                    {
                        OdooService.Client.UpdateModel("res.company", data, acc.res_company_id.Value, false);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw);
                    }
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var company = OdooService.Client.GetModel<resCompany>("res.company", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);
            
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                if (action == TransformType.CreateNew)
                {
                    var entry = new dboxBPKAccount()
                    {
                        Name = company.Name,
                        sosync_write_date = (company.Sosync_Write_Date ?? company.Write_Date).Value.ToLocalTime(),
                        res_company_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    try
                    {
                        db.Create(entry);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString());
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
                    acc.sosync_write_date = company.Sosync_Write_Date.Value.ToLocalTime();
                    acc.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(acc));

                    try
                    {
                        db.Update(acc);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString());
                        throw;
                    }
                }
            }
        }
        #endregion
    }
}
