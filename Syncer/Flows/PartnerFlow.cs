using dadi_data.Models;
using Odoo;
using Odoo.Models;
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
    [OnlineModel(Name = "res.partner")]
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
            return GetDefaultOnlineModelInfo(onlineID, "res.partner");
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
            ModelInfo result = null;

            dboPerson person = null;
            dboPersonOdooResPartner syncDetails = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            //using (var persOdoo = MdbService.GetDataService<dboPersonOdooResPartner>())
            //using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            //using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            //using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                // Load person and sosync write date
                person = personSvc.Read(new { PersonID = studioID }).SingleOrDefault();

                // Get the associated ids for the detail tables
                if (person != null)
                {
                    //syncDetails = persOdoo.Read(new { PersonID = person.PersonID }).SingleOrDefault();

                    //if (syncDetails != null)
                    //{
                    //    dboPersonAdresse address = null;
                    //    if (syncDetails.PersonAdresseID.HasValue)
                    //        address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).FirstOrDefault();

                    //    dboPersonEmail email = null;
                    //    if (syncDetails.PersonEmailID.HasValue)
                    //        email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).FirstOrDefault();

                    //    dboPersonTelefon phone = null;
                    //    if (syncDetails.PersonTelefonID.HasValue)
                    //        phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).FirstOrDefault();

                    //    // Get a combined write date, this considers all 4 entities, aswell as sosync_write_date and write_date

                    //    var combinedSosyncWriteDate = GetPersonSosyncWriteDate(person, address, email, phone);
                    //    var combinedWriteDate = GetPersonWriteDate(person, address, email, phone);

                    //    // Because GetPersonSosyncWriteDate already combines sosync_write_date and write_date,
                    //    // the combined_write_date can be used for both in the model info
                    //    result = new ModelInfo(studioID, syncDetails.res_partner_id, combinedSosyncWriteDate, combinedWriteDate);
                    //}
                    //else
                    //{
                    //    result = new ModelInfo(studioID, null, person.sosync_write_date, person.write_date);
                    //}

                    return new ModelInfo(studioID, person.sosync_fso_id, person.sosync_write_date, person.write_date);
                }
                else
                {
                    result = null;
                }

                return result;
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs required
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs required
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            dboPerson person = null;
            dboPersonAdresse address = null;
            dboPersonEmail email = null;
            dboPersonTelefon phone = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            //using (var persOdoo = MdbService.GetDataService<dboPersonOdooResPartner>())
            //using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            //using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            //using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                person = personSvc.Read(new { PersonID = studioID }).SingleOrDefault();
                //syncDetails = persOdoo.Read(new { PersonID = studioID }).SingleOrDefault();

                //if (syncDetails.PersonAdresseID.HasValue)
                //    address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).SingleOrDefault();

                //if (syncDetails.PersonEmailID.HasValue)
                //    email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).SingleOrDefault();

                //if (syncDetails.PersonTelefonID.HasValue)
                //    phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).SingleOrDefault();

                //var sosyncWriteDate = GetPersonSosyncWriteDate(person, address, email, phone);
                //var writeDate = GetPersonWriteDate(person, address, email, phone);

                var sosyncWriteDate = person.sosync_write_date;
                var writeDate = person.write_date;

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
                    { "birthdate_web", person.Geburtsdatum },
                    { "title_web", person.Titel },
                    { "BPKForcedFirstname", person.BPKErzwungenVorname },
                    { "BPKForcedLastname", person.BPKErzwungenNachname },
                    { "BPKForcedBirthdate", person.BPKErzwungenGeburtsdatum },
                    { "sosync_write_date", (sosyncWriteDate ?? writeDate).ToUniversalTime() }
                };

                // --> Country_ID --> über ISO-Code 

                if (action == TransformType.CreateNew)
                {
                    // On creation, also add the sosync_fs_id
                    data.Add("sosync_fs_id", person.PersonID);

                    int odooPartnerId = 0;
                    try
                    {
                        // Create res.partner
                        odooPartnerId = OdooService.Client.CreateModel("res.partner", data, false);
                    }
                    finally
                    {
                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw);
                    }

                    // Update the remote id in studio
                    //syncDetails.res_partner_id = odooPartnerId;
                    person.sosync_fso_id = odooPartnerId;
                    person.noSyncJobSwitch = true;

                    personSvc.Update(person);
                }
                else
                {
                    OdooService.Client.GetModel<resPartner>("res.partner", person.sosync_fso_id.Value);

                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                    try
                    {
                        // Update res.partner
                        OdooService.Client.UpdateModel("res.partner", data, person.sosync_fso_id.Value, false);
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
            var partner = OdooService.Client.GetModel<resPartner>("res.partner", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            dboPerson person = null;
            dboPersonAdresse address = null;
            dboPersonEmail email = null;
            dboPersonTelefon phone = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                // Load online model, save it to studio
                if (action == TransformType.CreateNew)
                {
                    person = new dboPerson
                    {
                        PersontypID = 101,
                        Anlagedatum = DateTime.Now,
                        create_date = DateTime.Now,
                        write_date = DateTime.Now,
                        sosync_fso_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    CopyPartnerToPerson(partner, person);
                    person.noSyncJobSwitch = true;

                    var requestData = new PersonCombined()
                    {
                        Person = person,
                        PersonAdresse = address,
                        PersonEmail = email,
                        PersonTelefon = phone,
                        WriteDateCombined = null,
                        SosyncWriteDateCombined = null
                    };

                    UpdateSyncTargetRequest(Serializer.ToXML(requestData));

                    try
                    {
                        personSvc.Create(person);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString());
                        throw;
                    }

                    OdooService.Client.UpdateModel(
                        "res.partner",
                        new { sosync_fs_id = person.PersonID },
                        onlineID,
                        false);
                }
                else
                {
                    person = personSvc.Read(new { PersonID = partner.Sosync_FS_ID }).SingleOrDefault();
                    //syncDetails = persOdoo.Read(new { PersonID = partner.Sosync_FS_ID }).SingleOrDefault();

                    //if (syncDetails.PersonAdresseID.HasValue)
                    //    address = addressSvc.Read(new { PersonAdresseID = syncDetails.PersonAdresseID }).SingleOrDefault();

                    //if (syncDetails.PersonEmailID.HasValue)
                    //    email = emailSvc.Read(new { PersonEmailID = syncDetails.PersonEmailID }).SingleOrDefault();

                    //if (syncDetails.PersonTelefonID.HasValue)
                    //    phone = phoneSvc.Read(new { PersonTelefonID = syncDetails.PersonTelefonID }).SingleOrDefault();

                    //var sosyncWriteDate = GetPersonSosyncWriteDate(person, address, email, phone);
                    //var writeDate = GetPersonWriteDate(person, address, email, phone);

                    var sosyncWriteDate = person.sosync_write_date;
                    var writeDate = person.write_date;

                    // PersonCombined is a helper class to combine all entities into one for
                    // serialization
                    var sourceData = new PersonCombined()
                    {
                        Person = person,
                        PersonAdresse = address,
                        PersonEmail = email,
                        PersonTelefon = phone,
                        WriteDateCombined = writeDate,
                        SosyncWriteDateCombined = sosyncWriteDate
                    };

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(sourceData));

                    CopyPartnerToPerson(partner, person);
                    person.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(sourceData));

                    try
                    {
                        personSvc.Update(person);
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

        private void CopyPartnerToPerson(resPartner source, dboPerson dest)
        {
            // Person data
            dest.Vorname = source.FirstName;
            dest.Name = source.LastName;
            dest.Name2 = source.Name_Zwei;
            dest.Geburtsdatum = source.Birthdate_Web;
            dest.Titel = source.Title_Web;
            dest.BPKErzwungenVorname = source.BPKForcedFirstname;
            dest.BPKErzwungenNachname = source.BPKForcedLastname;
            dest.BPKErzwungenGeburtsdatum = OdooConvert.ToDateTime(source.BPKForcedBirthdate);
            dest.sosync_write_date = source.Sosync_Write_Date.Value.ToLocalTime();
        }
        #endregion
    }
}
