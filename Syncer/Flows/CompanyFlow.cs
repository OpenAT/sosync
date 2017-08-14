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
            var dicCompany = Odoo.Client.GetDictionary("res.company", onlineID, new string[] { "id", "partner_id", "sosync_write_date" });
            var partnerID = Convert.ToInt32(((List<Object>)dicCompany["partner_id"])[0]);

            // A company is a partner, so to get the sosync_fs_id for the company, the corresponding partner is needed
            var dicPartner = Odoo.Client.GetDictionary("res.partner", partnerID, new string[] { "sosync_fs_id", "sosync_write_date" });

            // Pick sosync_fs_id from the partner entry of the company, and the write date from the company itself
            var fsIdStr = (string)dicPartner["sosync_fs_id"];
            var writeDateStr = (string)dicPartner["sosync_write_date"];

            if (!string.IsNullOrEmpty(fsIdStr) && Convert.ToInt32(fsIdStr) > 0)
                return new ModelInfo(onlineID, Convert.ToInt32(fsIdStr), DateTime.Parse(writeDateStr));
            else
                return new ModelInfo(onlineID, null, DateTime.Parse((string)dicPartner["sosync_write_date"]));
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            // A company in FS is just an entry in the xBPKAccount table.
            // So get the write date from that table
            dboxBPKAccount acc = null;

            using (var db = Mdb.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();
            }

            if (acc != null)
            {
                var writeDate = acc.sosync_write_date.HasValue ? acc.sosync_write_date.Value.ToUniversalTime() : (DateTime?)null;
                return new ModelInfo(studioID, acc.res_company_id, writeDate);
            }

            return null;
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboxBPKAccount acc = null;
            using (var db = Mdb.GetDataService<dboxBPKAccount>())
            {
                acc = db.Read(new { xBPKAccountID = studioID }).SingleOrDefault();

                if (action == TransformType.CreateNew)
                {
                    // Create the company
                    int odooCompanyId = Odoo.Client.CreateModel(
                        "res.company",
                        new { name = acc.Name, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                        false);

                    // Update the odoo company id in mssql
                    acc.res_company_id = odooCompanyId;
                    db.Update(acc);

                    // Load the newly created company to get the partner id
                    var company = Odoo.Client.GetModel<resCompany>("res.company", odooCompanyId);
                    var partnerID = Convert.ToInt32(company.Partner[0]);

                    // Update the sosync_fs_id on the partner
                    Odoo.Client.UpdateModel(
                        "res.partner",
                        new { sosync_fs_id = studioID },
                        partnerID,
                        false);
                }
                else
                {
                    // Request the current data from Odoo
                    var company = Odoo.Client.GetModel<resCompany>("res.company", acc.res_company_id.Value);
                    var partnerID = Convert.ToInt32(company.Partner[0]);

                    UpdateSyncTargetDataBeforeUpdate(Odoo.Client.LastResponseRaw);

                    Odoo.Client.UpdateModel(
                        "res.company",
                        new { name = acc.Name, sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                        acc.res_company_id.Value,
                        false);

                    // Only until res.company supports sosync_write_date directly
                    Odoo.Client.UpdateModel(
                        "res.partner",
                        new { sosync_write_date = acc.sosync_write_date.Value.ToUniversalTime() },
                        partnerID,
                        false);
                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var company = Odoo.Client.GetModel<resCompany>("res.company", onlineID);

            // A company is a partner, so to get the sosync_fs_id for the company, the corresponding partner is needed
            var dicPartner = Odoo.Client.GetDictionary(
                "res.partner",
                Convert.ToInt32(company.Partner[0]),
                new string[] { "sosync_fs_id", "sosync_write_date" });

            company.Sosync_Write_Date = Convert.ToDateTime(
                dicPartner["sosync_write_date"]).ToLocalTime();

            using (var db = Mdb.GetDataService<dboxBPKAccount>())
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
                    Odoo.Client.UpdateModel(
                        "res.partner",
                        new { sosync_fs_id = entry.xBPKAccountID, sosync_write_date = entry.sosync_write_date },
                        Convert.ToInt32(company.Partner[0]),
                        false);
                }
                else
                {
                    var sosync_fs_id = Convert.ToInt32(dicPartner["sosync_fs_id"]);
                    var acc = db.Read(new { xBPKAccountID = sosync_fs_id }).SingleOrDefault();
                    acc.Name = company.Name;
                    acc.sosync_write_date = company.Sosync_Write_Date;
                    db.Update(acc);
                }
            }
        }
        #endregion

        #region Methods
        #endregion
    }
}
