using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.Partner")]
    public class PartnerFlow : SyncFlow
    {
        #region Constructors
        public PartnerFlow(IServiceProvider svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var dic = Odoo.Client.GetDictionary("res.partner", 1, new string[] { "id", "sosync_fs_id", "write_date" });

            if (!string.IsNullOrEmpty((string)dic["sosync_fs_id"]))
                return new ModelInfo(onlineID, int.Parse((string)dic["sosync_fs_id"]), DateTime.Parse((string)dic["write_date"]));
            else
                return new ModelInfo(onlineID, null, DateTime.Parse((string)dic["write_date"]));
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            var writeDates = new List<DateTime>(3);

            dboPerson person = null;
            dboPersonOdooResPartner syncDetails = null;

            using (var personSvc = Mdb.GetDataService<dboPerson>())
            using (var persOdoo = Mdb.GetDataService<dboPersonOdooResPartner>())
            using (var addressSvc = Mdb.GetDataService<dboPersonAdresse>())
            using (var emailSvc = Mdb.GetDataService<dboPersonEmail>())
            using (var phoneSvc = Mdb.GetDataService<dboPersonTelefon>())
            {
                // Load person and sosync write date
                person = personSvc.Read(new { PersonID = studioID }).SingleOrDefault();
                writeDates.Add(person.sosync_write_date ?? DateTime.MinValue);

                // Get the associated ids for the detail tables
                syncDetails = persOdoo.Read(new { PersonID = person.PersonID }).SingleOrDefault();

                if (syncDetails != null)
                {
#warning TODO: replace the write dates with sosync_write_date once the database has the field
                    dboPersonAdresse address = null;
                    if (syncDetails.PersonAdresseID.HasValue)
                    {
                        address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).FirstOrDefault();
                        writeDates.Add(address.write_date);
                    }

                    dboPersonEmail email = null;
                    if (syncDetails.PersonEmailID.HasValue)
                    {
                        email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).FirstOrDefault();
                        writeDates.Add(email.write_date);
                    }

                    dboPersonTelefon phone = null;
                    if (syncDetails.PersonTelefonID.HasValue)
                    {
                        phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).FirstOrDefault();
                        writeDates.Add(phone.write_date);
                    }

                    return new ModelInfo(studioID, syncDetails.res_partner_id, writeDates.Max());
                }
                else
                {
                    throw new SyncerException($"dbo.Person {studioID} has no entry in dbo.PersonOdooResPartner.");
                }
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // Since this is a partner flow, onlineID represents the
            // res.partner id.

            // Use that partner id to get the company id
            var companyID = 0;

            // Tell the sync flow base, that this partner flow requires
            // the res.company
            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // Since this is a partner flow, onlineID represents the
            // dboPerson id.

            // Use the person id to get the xBPKAccount id
            var bpkAccount = 0;

            RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", bpkAccount);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            // Load studio model, save it to online
            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Load online model, save it to studio
            throw new NotImplementedException();
        }
        #endregion
    }
}
