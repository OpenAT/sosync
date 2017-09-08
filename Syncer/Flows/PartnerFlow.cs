using dadi_data;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Odoo;
using Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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

        #region Fields
        private ILogger<PartnerFlow> _log;
        #endregion

        #region Constructors
        public PartnerFlow(IServiceProvider svc)
            : base(svc)
        {
            _log = (ILogger<PartnerFlow>)svc.GetService(typeof(ILogger<PartnerFlow>));
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            return GetDefaultOnlineModelInfo(onlineID, "res.partner");
        }

        private DateTime? GetPersonWriteDate(dboPersonStack person)
        {
            var query = new DateTime?[]
            {
                        person.person != null ? person.person.write_date : (DateTime?)null,
                        person.address != null ? person.address.write_date : (DateTime?)null,
                        person.email != null ? person.email.write_date : (DateTime?)null,
                        person.phone != null ? person.phone.write_date : (DateTime?)null,
                        person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.write_date : (DateTime?)null,
                        person.emailNewsletter != null ? person.emailNewsletter.write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

        private DateTime? GetPersonSosyncWriteDate(dboPersonStack person)
        {
            var query = new DateTime?[]
            {
                person.person != null ? person.person.sosync_write_date : (DateTime?)null,
                person.address != null ? person.address.sosync_write_date : (DateTime?)null,
                person.email != null ? person.email.sosync_write_date : (DateTime?)null,
                person.phone != null ? person.phone.sosync_write_date : (DateTime?)null,
                person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.sosync_write_date : (DateTime?)null,
                person.emailNewsletter != null ? person.emailNewsletter.sosync_write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

        private dboPersonStack GetCurrentdboPersonStack(int PersonID)
        {
            dboPersonStack result = new dboPersonStack();

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                result.person = personSvc.Read(new { PersonID = PersonID }).FirstOrDefault();

                result.address = (from iterAddress in addressSvc.Read(new { PersonID = PersonID })
                                  where iterAddress.GültigVon <= DateTime.Today &&
                                      iterAddress.GültigBis >= DateTime.Today
                                  orderby string.IsNullOrEmpty(iterAddress.GültigMonatArray) ? "111111111111" : iterAddress.GültigMonatArray descending,
                                  iterAddress.PersonAdresseID descending
                                  select iterAddress).FirstOrDefault();

                if (result.address != null)
                {
                    result.addressAM = addressAMSvc.Read(new { PersonAdresseID = result.address.PersonAdresseID }).FirstOrDefault();
                }

                result.phone = (from iterPhone in phoneSvc.Read(new { PersonID = PersonID })
                                where iterPhone.GültigVon <= DateTime.Today &&
                                    iterPhone.GültigBis >= DateTime.Today
                                orderby string.IsNullOrEmpty(iterPhone.GültigMonatArray) ? "111111111111" : iterPhone.GültigMonatArray descending
                                select iterPhone).FirstOrDefault();

                result.email = (from iterEmail in emailSvc.Read(new { PersonID = PersonID })
                                where iterEmail.GültigVon <= DateTime.Today &&
                                    iterEmail.GültigBis >= DateTime.Today
                                orderby string.IsNullOrEmpty(iterEmail.GültigMonatArray) ? "111111111111" : iterEmail.GültigMonatArray descending
                                select iterEmail).FirstOrDefault();

                result.personDonationDeductionOptOut = personDonationDeductionOptOutSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 110493 }).FirstOrDefault();


                if (result.email != null)
                {
                    result.emailNewsletter = emailNewsletterSvc.Read(new { PersonEmailID = result.email.PersonEmailID, zGruppeDetailID = 30104 }).FirstOrDefault();
                }
            }

            result.write_date = GetPersonWriteDate(result);
            result.sosync_write_date = GetPersonSosyncWriteDate(result);

            return result;
        }

        /*
using (var personSvc = MdbService.GetDataService<dboPerson>(con, transaction))
using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>(con, transaction))
using (var emailSvc = MdbService.GetDataService<dboPersonEmail>(con, transaction))
using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>(con, transaction))
using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>(con, transaction))
{
}
*/

        private void SetdboPersonStack_fso_ids(
            dboPersonStack person,
            int onlineID,
            DataService<dboPerson> personSvc,
            DataService<dboPersonAdresse> addressSvc,
            DataService<dboPersonEmail> emailSvc,
            DataService<dboPersonTelefon> phoneSvc,
            DataService<dboPersonGruppe> personDonationDeductionOptOutSvc,
            DataService<dboPersonEmailGruppe> emailNewsletterSvc,
            SqlConnection con,
            SqlTransaction transaction)
        {
            if ((person.person.sosync_fso_id ?? 0) != onlineID)
            {
                person.person.sosync_fso_id = onlineID;
                person.person.noSyncJobSwitch = true;
                personSvc.Update(person.person);
            }

            foreach (dboPersonAdresse iterAddress in addressSvc.Read(new { PersonID = person.person.PersonID }))
            {
                if (person.address == null)
                {
                    if (iterAddress.sosync_fso_id != null)
                    {
                        iterAddress.sosync_fso_id = null;
                        iterAddress.noSyncJobSwitch = true;
                        addressSvc.Update(iterAddress);
                    }
                }
                else
                {
                    if (iterAddress.PersonAdresseID == person.address.PersonAdresseID)
                    {
                        if ((iterAddress.sosync_fso_id ?? 0) != onlineID)
                        {
                            iterAddress.sosync_fso_id = onlineID;
                            iterAddress.noSyncJobSwitch = true;
                            addressSvc.Update(iterAddress);
                        }
                    }
                    else
                    {
                        if (iterAddress.sosync_fso_id != null)
                        {
                            iterAddress.sosync_fso_id = null;
                            iterAddress.noSyncJobSwitch = true;
                            addressSvc.Update(iterAddress);
                        }
                    }
                }
            }

            foreach (dboPersonEmail iterEmail in emailSvc.Read(new { PersonID = person.person.PersonID }))
            {
                if (person.email == null)
                {
                    if (iterEmail.sosync_fso_id != null)
                    {
                        iterEmail.sosync_fso_id = null;
                        iterEmail.noSyncJobSwitch = true;
                        emailSvc.Update(iterEmail);
                    }
                }
                else
                {
                    if (iterEmail.PersonEmailID == person.email.PersonEmailID)
                    {
                        if ((iterEmail.sosync_fso_id ?? 0) != onlineID)
                        {
                            iterEmail.sosync_fso_id = onlineID;
                            iterEmail.noSyncJobSwitch = true;
                            emailSvc.Update(iterEmail);
                        }
                    }
                    else
                    {
                        if (iterEmail.sosync_fso_id != null)
                        {
                            iterEmail.sosync_fso_id = null;
                            iterEmail.noSyncJobSwitch = true;
                            emailSvc.Update(iterEmail);
                        }
                    }
                }
            }

            foreach (dboPersonTelefon iterPhone in phoneSvc.Read(new { PersonID = person.person.PersonID }))
            {
                if (person.phone == null)
                {
                    if (iterPhone.sosync_fso_id != null)
                    {
                        iterPhone.sosync_fso_id = null;
                        iterPhone.noSyncJobSwitch = true;
                        phoneSvc.Update(iterPhone);
                    }
                }
                else
                {
                    if (iterPhone.PersonTelefonID == person.phone.PersonTelefonID)
                    {
                        if ((iterPhone.sosync_fso_id ?? 0) != onlineID)
                        {
                            iterPhone.sosync_fso_id = onlineID;
                            iterPhone.noSyncJobSwitch = true;
                            phoneSvc.Update(iterPhone);
                        }
                    }
                    else
                    {
                        if (iterPhone.sosync_fso_id != null)
                        {
                            iterPhone.sosync_fso_id = null;
                            iterPhone.noSyncJobSwitch = true;
                            phoneSvc.Update(iterPhone);
                        }
                    }
                }
            }


            if (person.personDonationDeductionOptOut != null)
            {
                if ((person.personDonationDeductionOptOut.sosync_fso_id ?? 0) != onlineID)
                {
                    person.personDonationDeductionOptOut.sosync_fso_id = onlineID;
                    person.personDonationDeductionOptOut.noSyncJobSwitch = true;
                    personDonationDeductionOptOutSvc.Update(person.personDonationDeductionOptOut);
                }
            }

            if (person.emailNewsletter != null)
            {
                if ((person.emailNewsletter.sosync_fso_id ?? 0) != onlineID)
                {
                    person.emailNewsletter.sosync_fso_id = onlineID;
                    person.emailNewsletter.noSyncJobSwitch = true;
                    emailNewsletterSvc.Update(person.emailNewsletter);
                }
            }
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            ModelInfo result = null;
            dboPersonStack person = GetCurrentdboPersonStack(studioID);

            // Get the associated ids for the detail tables
            if (person.person != null)
                return new ModelInfo(
                    studioID,
                    person.person.sosync_fso_id,
                    GetPersonSosyncWriteDate(person),
                    GetPersonWriteDate(person));

            return result;
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

            var person = GetCurrentdboPersonStack(studioID);

            var sosync_write_date = (person.person.sosync_write_date ?? person.person.write_date.ToUniversalTime());

            var data = new Dictionary<string, object>()
                {
                    { "firstname", person.person.Vorname },
                    { "lastname", person.person.Name },
                    { "name_zwei", person.person.Name2 },
                    { "birthdate_web", person.person.Geburtsdatum },
                    { "title_web", person.person.Titel },
                    { "BPKForcedFirstname", person.person.BPKErzwungenVorname },
                    { "BPKForcedLastname", person.person.BPKErzwungenNachname },
                    { "BPKForcedBirthdate", person.person.BPKErzwungenGeburtsdatum },
                    { "BPKForcedZip", person.person.BPKErzwungenPLZ },
                    { "sosync_write_date", sosync_write_date }
                };

            if (person.address != null)
            {
                data.Add("street", person.address.Strasse);
                data.Add("street_number_web", person.address.Hausnummer);
                data.Add("zip", person.address.PLZ);
                data.Add("city", person.address.Ort);
            }
            else
            {
                data.Add("street", null);
                data.Add("street_number_web", null);
                data.Add("zip", null);
                data.Add("city", null);
            }

            if (person.email != null)
            {
                data.Add("email", person.email.EmailVor + "@" + person.email.EmailNach);
            }
            else
            {
                data.Add("email", null);
            }

            if (person.phone != null)
            {
                data.Add("phone", (person.phone.Landkennzeichen + " " + person.phone.Vorwahl + " " + person.phone.Rufnummer).Trim());
            }
            else
            {
                data.Add("phone", null);
            }

            if (person.personDonationDeductionOptOut != null)
            {
                if (person.personDonationDeductionOptOut.GültigVon <= DateTime.Today
                    && person.personDonationDeductionOptOut.GültigBis >= DateTime.Today)
                {
                    data.Add("donation_deduction_optout_web", true);
                }
                else
                {
                    data.Add("donation_deduction_optout_web", false);
                }
            }

            if (person.emailNewsletter != null)
            {
                if (person.emailNewsletter.GültigVon <= DateTime.Today
                    && person.emailNewsletter.GültigBis >= DateTime.Today)
                {
                    data.Add("newsletter_web", true);
                }
                else
                {
                    data.Add("newsletter_web", false);
                }
            }

            
            UpdateSyncSourceData(Serializer.ToXML(person));
            

            // --> Country_ID --> über ISO-Code 

            if (action == TransformType.CreateNew)
            {
                // On creation, also add the sosync_fs_id
                data.Add("sosync_fs_id", person.person.PersonID);

                int odooPartnerId = 0;
                try
                {
                    // Create res.partner
                    odooPartnerId = OdooService.Client.CreateModel("res.partner", data, false);
                }
                finally
                {
                    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooPartnerId);
                }

                // Update the remote id in studio
                using (var personSvc = MdbService.GetDataService<dboPerson>())
                using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
                using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
                using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
                using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
                using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
                {
                    SetdboPersonStack_fso_ids(
                        person, 
                        odooPartnerId, 
                        personSvc, 
                        addressSvc, 
                        emailSvc, 
                        phoneSvc, 
                        personDonationDeductionOptOutSvc, 
                        emailNewsletterSvc, 
                        null, 
                        null);
                }
            }
            else
            {
                OdooService.Client.GetModel<resPartner>("res.partner", person.person.sosync_fso_id.Value);

                UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
                try
                {
                    // Update res.partner
                    OdooService.Client.UpdateModel("res.partner", data, person.person.sosync_fso_id.Value, false);
                }
                finally
                {
                    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
                }
            }
            
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var partner = OdooService.Client.GetModel<resPartner>("res.partner", onlineID);
            var sosync_write_date = (partner.Sosync_Write_Date ?? partner.Write_Date).Value;

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            {
                var transaction = personSvc.BeginTransaction();
                var con = personSvc.Connection;

                using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>(con, transaction))
                using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>(con, transaction))
                using (var emailSvc = MdbService.GetDataService<dboPersonEmail>(con, transaction))
                using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
                using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>(con, transaction))
                using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>(con, transaction))
                {
                    // Load online model, save it to studio
                    if (action == TransformType.CreateNew)
                    {
                        var person = new dboPersonStack();

                        //create new dboPerson
                        person.person = InitDboPerson();

                        person.person.sosync_fso_id = onlineID;
                        person.person.sosync_write_date = sosync_write_date;
                        person.person.noSyncJobSwitch = true;

                        CopyPartnerToPerson(partner, person.person);

                        //create new dboPersonAddress if any associated field has a value
                        if (PartnerHasAddress(partner))
                        {
                            person.address = InitDboPersonAdresse();

                            person.address.sosync_fso_id = onlineID;
                            person.address.sosync_write_date = sosync_write_date;
                            person.address.noSyncJobSwitch = true;

                            CopyPartnerToPersonAddress(partner, person.address);

                            person.addressAM = InitDboPersonAdresseAM();


                        }

                        //create new dboPersonEmail if email is filled
                        if (PartnerHasEmail(partner))
                        {

                            person.email = InitDboPersonEmail();

                            person.email.sosync_fso_id = onlineID;

                            person.email.sosync_write_date = sosync_write_date;
                            person.email.noSyncJobSwitch = true;

                            CopyPartnerToPersonEmail(partner, person.email);

                        }

                        //create new dboPersonTelefon if phone is filled
                        if (PartnerHasPhone(partner))
                        {
                            person.phone = InitDboPersonTelefon();

                            person.phone.sosync_fso_id = onlineID;

                            person.phone.sosync_write_date = sosync_write_date;
                            person.phone.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefon(partner, person.phone);

                        }

                        //create new dboPersonGruppe if don-de-oo
                        if (partner.DonationDeductionOptOut ?? false)
                        {
                            person.personDonationDeductionOptOut = InitPersonDonationDeductionOptOut();

                            person.personDonationDeductionOptOut.sosync_fso_id = onlineID;

                            person.personDonationDeductionOptOut.sosync_write_date = sosync_write_date;
                            person.personDonationDeductionOptOut.noSyncJobSwitch = true;

                        }

                        if (partner.EmailNewsletter && PartnerHasEmail(partner))
                        {
                            person.emailNewsletter = InitEmailNewsletter();

                            person.emailNewsletter.sosync_fso_id = onlineID;

                            person.emailNewsletter.sosync_write_date = sosync_write_date;
                            person.emailNewsletter.noSyncJobSwitch = true;

                        }


                        UpdateSyncTargetRequest(Serializer.ToXML(person));

                        var PersonID = 0;
                        try
                        {
                            personSvc.Create(person.person);

                             PersonID = person.person.PersonID;

                            if (person.address != null)
                            {
                                person.address.PersonID = PersonID;
                                addressSvc.Create(person.address);
                                person.addressAM.PersonID = PersonID;
                                person.addressAM.PersonAdresseID = person.address.PersonAdresseID;
                                addressAMSvc.Create(person.addressAM);
                            }

                            if (person.email != null)
                            {
                                person.email.PersonID = PersonID;
                                emailSvc.Create(person.email);

                                if (person.emailNewsletter != null)
                                {
                                    person.emailNewsletter.PersonEmailID = person.email.PersonEmailID;
                                    emailNewsletterSvc.Create(person.emailNewsletter);
                                }

                            }

                            if (person.phone != null)
                            {
                                person.phone.PersonID = PersonID;
                                phoneSvc.Create(person.phone);
                            }

                            if (person.personDonationDeductionOptOut != null)
                            {
                                person.personDonationDeductionOptOut.PersonID = PersonID;
                                personDonationDeductionOptOutSvc.Create(person.personDonationDeductionOptOut);
                            }

                            UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, PersonID);
                        }
                        catch (Exception ex)
                        {
                            UpdateSyncTargetAnswer(ex.ToString(), PersonID);
                            throw;
                        }

                        OdooService.Client.UpdateModel(
                            "res.partner",
                            new { sosync_fs_id = person.person.PersonID },
                            onlineID,
                            false);
                    }
                    else
                    {

                        var PersonID = partner.Sosync_FS_ID;

                        var person = GetCurrentdboPersonStack(PersonID);


                        UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(person));

                        CopyPartnerToPerson(partner, person.person);

                        person.person.sosync_fso_id = onlineID;
                        person.person.sosync_write_date = sosync_write_date;
                        person.person.noSyncJobSwitch = true;

                        if (person.address == null)
                        {
                            if (PartnerHasAddress(partner))
                            {
                                person.address = InitDboPersonAdresse();
                                person.addressAM = InitDboPersonAdresseAM();

                                person.address.PersonID = PersonID;
                                person.addressAM.PersonID = PersonID;

                                CopyPartnerToPersonAddress(partner, person.address);

                                person.address.sosync_fso_id = onlineID;
                                person.address.sosync_write_date = sosync_write_date;
                                person.address.noSyncJobSwitch = true;

                            }
                        }
                        else
                        {
                            CopyPartnerToPersonAddress(partner, person.address);

                            person.address.sosync_fso_id = onlineID;
                            person.address.sosync_write_date = sosync_write_date;
                            person.address.noSyncJobSwitch = true;

                            person.addressAM.PAC = 0; //personadresseAM record is always created with the personadresse record, so it can be refered here

                        }

                        if (person.email == null)
                        {
                            if (PartnerHasEmail(partner))
                            {
                                person.email = InitDboPersonEmail();

                                person.email.PersonID = PersonID;

                                CopyPartnerToPersonEmail(partner, person.email);

                                person.email.sosync_fso_id = onlineID;
                                person.email.sosync_write_date = sosync_write_date;
                                person.email.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {

                            CopyPartnerToPersonEmail(partner, person.email);

                            person.email.sosync_fso_id = onlineID;
                            person.email.sosync_write_date = sosync_write_date;
                            person.email.noSyncJobSwitch = true;

                        }

                        if (person.phone == null)
                        {
                            if (PartnerHasPhone(partner))
                            {
                                person.phone = InitDboPersonTelefon();

                                person.phone.PersonID = PersonID;

                                CopyPartnerToPersonTelefon(partner, person.phone);

                                person.phone.sosync_fso_id = onlineID;
                                person.phone.sosync_write_date = sosync_write_date;
                                person.phone.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {

                            CopyPartnerToPersonTelefon(partner, person.phone);

                            person.email.sosync_fso_id = onlineID;
                            person.email.sosync_write_date = sosync_write_date;
                            person.email.noSyncJobSwitch = true;

                        }

                        if (person.personDonationDeductionOptOut == null)
                        {
                            if (partner.DonationDeductionOptOut ?? false)
                            {
                                person.personDonationDeductionOptOut = InitPersonDonationDeductionOptOut();

                                person.personDonationDeductionOptOut.PersonID = PersonID;

                                person.personDonationDeductionOptOut.sosync_fso_id = onlineID;
                                person.personDonationDeductionOptOut.sosync_write_date = sosync_write_date;
                                person.personDonationDeductionOptOut.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            if (partner.DonationDeductionOptOut ?? false)
                            {
                                if (person.personDonationDeductionOptOut.GültigVon > DateTime.Today)
                                {
                                    person.personDonationDeductionOptOut.GültigVon = DateTime.Today;
                                }
                                person.personDonationDeductionOptOut.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.personDonationDeductionOptOut.GültigVon > DateTime.Today)
                                {
                                    person.personDonationDeductionOptOut.GültigVon = DateTime.Today;
                                }
                                person.personDonationDeductionOptOut.GültigBis = DateTime.Today;
                            }

                            person.personDonationDeductionOptOut.sosync_fso_id = onlineID;
                            person.personDonationDeductionOptOut.sosync_write_date = sosync_write_date;
                            person.personDonationDeductionOptOut.noSyncJobSwitch = true;
                        }





                        if (person.emailNewsletter == null)
                        {
                            if (partner.EmailNewsletter && person.email != null)
                            {
                                person.emailNewsletter = InitEmailNewsletter();

                                person.emailNewsletter.PersonEmailID = person.email.PersonEmailID;

                                person.emailNewsletter.sosync_fso_id = onlineID;
                                person.emailNewsletter.sosync_write_date = sosync_write_date;
                                person.emailNewsletter.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            if (partner.EmailNewsletter)
                            {
                                if (person.emailNewsletter.GültigVon > DateTime.Today)
                                {
                                    person.emailNewsletter.GültigVon = DateTime.Today;
                                }
                                person.emailNewsletter.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.emailNewsletter.GültigVon > DateTime.Today)
                                {
                                    person.emailNewsletter.GültigVon = DateTime.Today;
                                }
                                person.emailNewsletter.GültigBis = DateTime.Today;
                            }

                            person.emailNewsletter.sosync_fso_id = onlineID;
                            person.emailNewsletter.sosync_write_date = sosync_write_date;
                            person.emailNewsletter.noSyncJobSwitch = true;
                        }

                        UpdateSyncTargetRequest(Serializer.ToXML(person));

                        try
                        {

                            personSvc.Update(person.person);

                            if (person.address != null)
                            {
                                if (person.address.PersonAdresseID == 0)
                                {
                                    addressSvc.Create(person.address);
                                    person.addressAM.PersonAdresseID = person.address.PersonAdresseID;
                                    addressAMSvc.Create(person.addressAM);
                                }
                                else
                                {
                                    addressSvc.Update(person.address);
                                    addressAMSvc.Update(person.addressAM);
                                }
                            }

                            if (person.email != null)
                            {
                                if (person.email.PersonEmailID == 0)
                                {
                                    emailSvc.Create(person.email);
                                }
                                else
                                {
                                    emailSvc.Update(person.email);
                                }
                            }

                            if (person.phone != null)
                            {
                                if (person.phone.PersonTelefonID == 0)
                                {
                                    phoneSvc.Create(person.phone);
                                }
                                else
                                {
                                    phoneSvc.Update(person.phone);
                                }
                            }

                            if (person.personDonationDeductionOptOut != null)
                            {
                                if (person.personDonationDeductionOptOut.PersonGruppeID == 0)
                                {
                                    personDonationDeductionOptOutSvc.Create(person.personDonationDeductionOptOut);
                                }
                                else
                                {
                                    personDonationDeductionOptOutSvc.Update(person.personDonationDeductionOptOut);
                                }
                            }

                            if (person.emailNewsletter != null)
                            {
                                if (person.emailNewsletter.PersonEmailGruppeID == 0)
                                {
                                    emailNewsletterSvc.Create(person.emailNewsletter);
                                }
                                else
                                {
                                    emailNewsletterSvc.Update(person.emailNewsletter);
                                }
                            }

                            SetdboPersonStack_fso_ids(
                               person,
                               onlineID,
                               personSvc,
                               addressSvc,
                               emailSvc,
                               phoneSvc,
                               personDonationDeductionOptOutSvc,
                               emailNewsletterSvc,
                               personSvc.Connection,
                               transaction);

                            UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
                        }
                        catch (Exception ex)
                        {
                            UpdateSyncTargetAnswer(ex.ToString(), null);
                            throw;
                        }
                    }
                    personSvc.CommitTransaction();
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
        }
        private void CopyPartnerToPersonAddress(resPartner source, dboPersonAdresse dest)
        {
            dest.Strasse = source.Street;
            dest.Hausnummer = source.StreetNumber;
            dest.PLZ = source.Zip;
            dest.Ort = source.City;
        }

        private void CopyPartnerToPersonEmail(resPartner source, dboPersonEmail dest)
        {
            if(string.IsNullOrEmpty(source.Email))
            {
                dest.EmailVor = null;
                dest.EmailNach = null;
            }
            else if(source.Email.Contains("@"))
            {
                var emailParts = source.Email.Split('@');
                dest.EmailVor = emailParts[0];
                dest.EmailNach = emailParts[1];
            }
            else
            {
                dest.EmailVor = source.Email;
                dest.EmailNach = "";
            }
            
        }

        private void CopyPartnerToPersonTelefon(resPartner source, dboPersonTelefon dest)
        {
            //TODO: parse phone into its parts
            dest.Landkennzeichen = "";
            dest.Vorwahl = "";
            dest.Rufnummer = source.Phone;


        }


        private const string GültigMonatArray = "111111111111";
        private dboPerson InitDboPerson()
        {
            dboPerson result = null;

            using (var typenSvc = MdbService.GetDataService<dboTypen>())
            {
                
                var defaultLandID = (int)typenSvc.Read(new { TypenID = 200525 }).FirstOrDefault().Formularwert;

                result = new dboPerson
                {
                    PersontypID = 101,
                    NationalitätID = defaultLandID,
                    GeschlechttypID = 0,
                    zMarketingID = 0,
                    Anlagedatum = DateTime.Now,
                    xIDA = 0,
                    SprachetypID = 0
                };

            }
            return result;

        }
        private dboPersonAdresse InitDboPersonAdresse()
        {
            dboPersonAdresse result = null;

            using (var typenSvc = MdbService.GetDataService<dboTypen>())
            {

                var defaultAnredefromtypID = (int)typenSvc.Read(new { TypenID = 200120 }).FirstOrDefault().Formularwert;
                var defaultLandID = (int)typenSvc.Read(new { TypenID = 200525 }).FirstOrDefault().Formularwert;

                result = new dboPersonAdresse
                {
                    AdresstypID = 311, //static "Hauptanschrift"
                    AnredeformtypID = defaultAnredefromtypID,
                    LandID = defaultLandID,
                    FehlerZähler = 0,
                    GültigMonatArray = GültigMonatArray,
                    GültigVon = DateTime.Today,
                    GültigBis = new DateTime(2099, 12, 31),
                    xIDA = 0
                };

            }
            return result;

        }

        private dboPersonAdresseAM InitDboPersonAdresseAM()
        {

            dboPersonAdresseAM result = null;

            result = new dboPersonAdresseAM
            {

                PAC = 0,
                xIDA = 0
            };

            return result;

        }

        private dboPersonEmail InitDboPersonEmail()
        {
            dboPersonEmail result = null;

            using (var typenSvc = MdbService.GetDataService<dboTypen>())
            {

                var defaultEmailAnredefromtypID = (int)typenSvc.Read(new { TypenID = 200122 }).FirstOrDefault().Formularwert;

                result = new dboPersonEmail
                {
                    AnredeformtypID = defaultEmailAnredefromtypID,
                    GültigMonatArray = GültigMonatArray,
                    GültigVon = DateTime.Today,
                    GültigBis = new DateTime(2099, 12, 31)

                };

            }
            return result;

        }

        private dboPersonTelefon InitDboPersonTelefon()
        {
            dboPersonTelefon result = null;


            result = new dboPersonTelefon
            {
                TelefontypID = 0,
                GültigMonatArray = GültigMonatArray,
                GültigVon = DateTime.Today,
                GültigBis = new DateTime(2099, 12, 31)

            };


            return result;

        }

        private dboPersonGruppe InitPersonDonationDeductionOptOut()
        {
            dboPersonGruppe result = null;


            result = new dboPersonGruppe
            {
                zGruppeDetailID = 110493,
                Steuerung = true,
                GültigVon = DateTime.Today,
                GültigBis = new DateTime(2099, 12, 31),
                xIDA = 0

            };


            return result;

        }

        private dboPersonEmailGruppe InitEmailNewsletter()
        {
            dboPersonEmailGruppe result = null;


            result = new dboPersonEmailGruppe
            {
                zGruppeDetailID = 30104,
                Steuerung = true,
                GültigVon = DateTime.Today,
                GültigBis = new DateTime(2099, 12, 31),
                xIDA = 0
            };


            return result;

        }

        private bool PartnerHasAddress(resPartner partner)
        {
            return (!string.IsNullOrEmpty( partner.Street) || !string.IsNullOrEmpty(partner.StreetNumber) || !string.IsNullOrEmpty(partner.Zip) || !string.IsNullOrEmpty(partner.City));
        }

        private bool PartnerHasEmail(resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Email));
        }

        private bool PartnerHasPhone(resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Phone));
        }

        #endregion
    }
}
