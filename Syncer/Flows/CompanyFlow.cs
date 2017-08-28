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
                new string[] { "id", "sosync_fs_id", "sosync_write_date" });

            if (!OdooService.Client.IsValidResult(dicCompany))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, "res.company", onlineID);

            var fsID = OdooConvert.ToInt32((string)dicCompany["sosync_fs_id"]);
            var writeDate = OdooConvert.ToDateTime((string)dicCompany["sosync_write_date"]);

            return new ModelInfo(onlineID, fsID, writeDate);
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
                var writeDate = acc.sosync_write_date;
                return new ModelInfo(studioID, acc.res_company_id, writeDate);
            }

            return null;
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboxBPKAccount acc = null;
            using (var db = MdbService.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();

                if (action == TransformType.CreateNew)
                {
                    // Create the company
                    int odooCompanyId = OdooService.Client.CreateModel(
                        "res.company",
                        new { name = acc.Name, sosync_fs_id = acc.xBPKAccountID, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                        false);

                    // Update the odoo company id in mssql
                    acc.res_company_id = odooCompanyId;
                    db.Update(acc);
                }
                else
                {
                    // Request the current data from Odoo
                    var company = OdooService.Client.GetModel<resCompany>("res.company", acc.res_company_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);

                    OdooService.Client.UpdateModel(
                        "res.company",
                        new { name = acc.Name, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                        acc.res_company_id.Value,
                        false);
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var company = OdooService.Client.GetModel<resCompany>("res.company", onlineID);

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

                    db.Create(entry);
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
                    acc.Name = company.Name;
                    acc.sosync_write_date = company.Sosync_Write_Date.Value.ToLocalTime();
                    db.Update(acc);
                }
            }
        }
        #endregion

        #region Methods
        #endregion
    }
}
