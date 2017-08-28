using dadi_data.Models;
using Odoo;
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
            var dicPartner = OdooService.Client.GetDictionary("res.partner", onlineID, new string[] { "id", "sosync_fs_id", "sosync_write_date" });

            if (!OdooService.Client.IsValidResult(dicPartner))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, "res.partner", onlineID);

            var fsID = OdooConvert.ToInt32((string)dicPartner["sosync_fs_id"]);
            var writeDate = OdooConvert.ToDateTime((string)dicPartner["sosync_write_date"]);

            return new ModelInfo(onlineID, fsID, writeDate);
        }

        private DateTime? GetPersonSosyncWriteDate(dboPerson person, dboPersonAdresse address, dboPersonEmail mail, dboPersonTelefon phone)
        {
            var writeDates = new List<DateTime>();

            if (person.sosync_write_date.HasValue)
                writeDates.Add(person.sosync_write_date.Value);

            if (address != null)
                writeDates.Add(address.write_date);

            if (mail != null)
                writeDates.Add(mail.write_date);

            if (phone != null)
                writeDates.Add(phone.write_date);


            if (writeDates.Count == 0)
                return null;

            return writeDates.Max();
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {

            dboPerson person = null;
            dboPersonOdooResPartner syncDetails = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var persOdoo = MdbService.GetDataService<dboPersonOdooResPartner>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                // Load person and sosync write date
                person = personSvc.Read(new { PersonID = studioID }).SingleOrDefault();

                // Get the associated ids for the detail tables
                syncDetails = persOdoo.Read(new { PersonID = person.PersonID }).SingleOrDefault();

                if (syncDetails != null)
                {
#warning TODO: replace the write dates with sosync_write_date once the database has the field
                    dboPersonAdresse address = null;
                    if (syncDetails.PersonAdresseID.HasValue)
                    {
                        address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).FirstOrDefault();
                    }

                    dboPersonEmail email = null;
                    if (syncDetails.PersonEmailID.HasValue)
                    {
                        email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).FirstOrDefault();
                    }

                    dboPersonTelefon phone = null;
                    if (syncDetails.PersonTelefonID.HasValue)
                    {
                        phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).FirstOrDefault();
                    }

                    var sosync_write_date = GetPersonSosyncWriteDate(person, address, email, phone);

                    return new ModelInfo(studioID, syncDetails.res_partner_id, sosync_write_date);
                }
                else
                {
                    throw new SyncerException($"dbo.Person {studioID} has no entry in dbo.PersonOdooResPartner.");
                }
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {

        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {

        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboPerson person = null;
            dboPersonOdooResPartner syncDetails = null;
            dboPersonAdresse address = null;
            dboPersonEmail email = null;
            dboPersonTelefon phone = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var persOdoo = MdbService.GetDataService<dboPersonOdooResPartner>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                person = personSvc.Read(new { PersonID = studioID }).SingleOrDefault();
                syncDetails = persOdoo.Read(new { PersonID = studioID }).SingleOrDefault();

                if (syncDetails.PersonAdresseID.HasValue)
                    address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).SingleOrDefault();

                if (syncDetails.PersonEmailID.HasValue)
                    email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).SingleOrDefault();

                if (syncDetails.PersonTelefonID.HasValue)
                    phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).SingleOrDefault();

                var sosync_write_date = GetPersonSosyncWriteDate(person, address, email, phone);

                if (action == TransformType.CreateNew)
                {
                    var data = new Dictionary<string, object>();

                    data.Add("firstname", OdooFormat.ToString(person.Vorname));
                    data.Add("lastname", OdooFormat.ToString(person.Name));
                    data.Add("name_zwei", OdooFormat.ToString(person.Name2));
                    data.Add("sosync_fs_id", person.PersonID);
                    data.Add("sosync_write_date", OdooFormat.ToDateTime(sosync_write_date.Value.ToUniversalTime()));

                    // Create res.partner
                    int odooPartnerId = OdooService.Client.CreateModel("res.partner", data, false);

                    // Update the remote id in studio
                    syncDetails.res_partner_id = odooPartnerId;
                    persOdoo.Update(syncDetails);
                }
                else
                {
                    // Update res.partner

                }
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Load online model, save it to studio
            throw new NotImplementedException();
        }
        #endregion
    }
}
