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
            var dicPartner = OdooService.Client.GetDictionary("res.partner", onlineID, new string[] { "id", "sosync_fs_id", "write_date", "sosync_write_date" });

            if (!OdooService.Client.IsValidResult(dicPartner))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, "res.partner", onlineID);

            var fsID = OdooConvert.ToInt32((string)dicPartner["sosync_fs_id"]);
            var sosyncWriteDate = OdooConvert.ToDateTime((string)dicPartner["sosync_write_date"]);
            var writeDate = OdooConvert.ToDateTime((string)dicPartner["write_date"]);

            return new ModelInfo(onlineID, fsID, sosyncWriteDate, writeDate);
        }

        private DateTime? GetPersonWriteDate(dboPerson person, dboPersonAdresse address, dboPersonEmail email, dboPersonTelefon phone)
        {
            var query = new DateTime?[]
            {
                        person != null ? person.write_date : (DateTime?)null,
                        address != null ? address.write_date : (DateTime?)null,
                        email != null ? email.write_date : (DateTime?)null,
                        phone != null ? phone.write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

        private DateTime? GetPersonSosyncWriteDate(dboPerson person, dboPersonAdresse address, dboPersonEmail email, dboPersonTelefon phone)
        {
#warning TODO: replace write_date with sosync_write_date once database has it on all models
            var query = new DateTime?[]
            {
                        person != null ? person.sosync_write_date : (DateTime?)null,
                        address != null ? address.write_date : (DateTime?)null,
                        email != null ? email.write_date : (DateTime?)null,
                        phone != null ? phone.write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
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
                    dboPersonAdresse address = null;
                    if (syncDetails.PersonAdresseID.HasValue)
                        address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).FirstOrDefault();

                    dboPersonEmail email = null;
                    if (syncDetails.PersonEmailID.HasValue)
                        email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).FirstOrDefault();

                    dboPersonTelefon phone = null;
                    if (syncDetails.PersonTelefonID.HasValue)
                        phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).FirstOrDefault();

                    // Get a combined write date, this considers all 4 entities, aswell as sosync_write_date and write_date

                    var combinedSosyncWriteDate = GetPersonSosyncWriteDate(person, address, email, phone);
                    var combinedWriteDate = GetPersonWriteDate(person, address, email, phone);

                    // Because GetPersonSosyncWriteDate already combines sosync_write_date and write_date,
                    // the combined_write_date can be used for both in the model info
                    return new ModelInfo(studioID, syncDetails.res_partner_id, combinedSosyncWriteDate, combinedWriteDate);
                }
                else
                {
                    throw new SyncerException($"dbo.Person {studioID} has no entry in dbo.PersonOdooResPartner.");
                }
            }
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

                var sosyncWriteDate = GetPersonSosyncWriteDate(person, address, email, phone);
                var writeDate = GetPersonWriteDate(person, address, email, phone);

                var sourceData = new PersonCombined()
                {
                    Person = person,
                    PersonAdresse = address,
                    PersonEmail = email,
                    PersonTelefon = phone,
                    WriteDateCombined = writeDate,
                    SosyncWriteDateCombined = sosyncWriteDate
                };

                UpdateSyncSourceData(Serializer.ToXML(sourceData));

                // Perpare data that is the same for create or update
                var data = new Dictionary<string, object>()
                {
                    { "firstname", person.Vorname },
                    { "lastname", person.Name },
                    { "name_zwei", person.Name2 },
                    { "sosync_write_date",  OdooFormat.ToDateTime((sosyncWriteDate ?? writeDate).Value.ToUniversalTime()) }
                };

                if (action == TransformType.CreateNew)
                {
                    // On creation, also add the sosync_fs_id
                    data.Add("sosync_fs_id", person.PersonID);

                    // Create res.partner
                    int odooPartnerId = OdooService.Client.CreateModel("res.partner", data, false);

                    // Update the remote id in studio
                    syncDetails.res_partner_id = odooPartnerId;
                    persOdoo.Update(syncDetails);
                }
                else
                {
                    // Update res.partner
                    OdooService.Client.UpdateModel("res.partner", data, syncDetails.res_partner_id.Value, false);
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
