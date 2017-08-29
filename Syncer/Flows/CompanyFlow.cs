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
            // Get company ids and write date
            var dicCompany = OdooService.Client.GetDictionary(
                "res.company",
                onlineID,
                new string[] { "id", "sosync_fs_id", "write_date", "sosync_write_date" });

            if (!OdooService.Client.IsValidResult(dicCompany))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, "res.company", onlineID);

            var fsID = OdooConvert.ToInt32((string)dicCompany["sosync_fs_id"]);
            var syncWriteDate = OdooConvert.ToDateTime((string)dicCompany["sosync_write_date"]);

            return new ModelInfo(onlineID, fsID, syncWriteDate);
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // A company in FS is just an entry in the xBPKAccount table.
            // So get the write date from that table
            dboxBPKAccount acc = null;

            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();
            }

            if (acc != null)
            {
                return new ModelInfo(studioID, acc.res_company_id, acc.sosync_write_date);
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

                if (action == TransformType.CreateNew)
                {
                    // Create the company
                    try
                    {
                        int odooCompanyId = OdooService.Client.CreateModel(
                            "res.company",
                            new { name = acc.Name, sosync_fs_id = acc.xBPKAccountID, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                            false);

                        // Update the odoo company id in mssql
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
                    // Request the current data from Odoo
                    var company = OdooService.Client.GetModel<resCompany>("res.company", acc.res_company_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

                    try
                    {
                        OdooService.Client.UpdateModel(
                            "res.company",
                            new { name = acc.Name, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                            acc.res_company_id.Value,
                            false);
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
                        sosync_write_date = company.Sosync_Write_Date,
                        res_company_id = onlineID
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
