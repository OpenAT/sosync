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
                person.mobile != null ? person.mobile.write_date : (DateTime?)null,
                person.fax != null ? person.fax.write_date : (DateTime?)null,
                person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.write_date : (DateTime?)null,
                person.emailNewsletter != null ? person.emailNewsletter.write_date : (DateTime?)null,
                person.personDonationReceipt != null ? person.personDonationReceipt.write_date : (DateTime?)null
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
                person.mobile != null ? person.mobile.sosync_write_date : (DateTime?)null,
                person.fax != null ? person.fax.sosync_write_date : (DateTime?)null,
                person.personDonationDeductionOptOut != null ? person.personDonationDeductionOptOut.sosync_write_date : (DateTime?)null,
                person.emailNewsletter != null ? person.emailNewsletter.sosync_write_date : (DateTime?)null,
                person.personDonationReceipt != null ? person.personDonationReceipt.sosync_write_date : (DateTime?)null
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
            using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>())
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
                                    iterPhone.GültigBis >= DateTime.Today &&
                                    iterPhone.TelefontypID == 400
                                orderby string.IsNullOrEmpty(iterPhone.GültigMonatArray) ? "111111111111" : iterPhone.GültigMonatArray descending
                                select iterPhone).FirstOrDefault();

                result.mobile = (from itermobile in mobileSvc.Read(new { PersonID = PersonID })
                                where itermobile.GültigVon <= DateTime.Today &&
                                    itermobile.GültigBis >= DateTime.Today &&
                                    itermobile.TelefontypID == 401
                                orderby string.IsNullOrEmpty(itermobile.GültigMonatArray) ? "111111111111" : itermobile.GültigMonatArray descending
                                select itermobile).FirstOrDefault();

                result.fax = (from iterfax in faxSvc.Read(new { PersonID = PersonID })
                                where iterfax.GültigVon <= DateTime.Today &&
                                    iterfax.GültigBis >= DateTime.Today &&
                                    iterfax.TelefontypID == 403
                                orderby string.IsNullOrEmpty(iterfax.GültigMonatArray) ? "111111111111" : iterfax.GültigMonatArray descending
                                select iterfax).FirstOrDefault();

                result.email = (from iterEmail in emailSvc.Read(new { PersonID = PersonID })
                                where iterEmail.GültigVon <= DateTime.Today &&
                                    iterEmail.GültigBis >= DateTime.Today
                                orderby string.IsNullOrEmpty(iterEmail.GültigMonatArray) ? "111111111111" : iterEmail.GültigMonatArray descending
                                select iterEmail).FirstOrDefault();

                result.personDonationDeductionOptOut = personDonationDeductionOptOutSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 110493 }).FirstOrDefault();

                result.personDonationReceipt = personDonationReceiptSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 20168 }).FirstOrDefault();


                if (result.email != null)
                {
                    result.emailNewsletter = emailNewsletterSvc.Read(new { PersonEmailID = result.email.PersonEmailID, zGruppeDetailID = 30104 }).FirstOrDefault();
                }
            }

            result.write_date = GetPersonWriteDate(result);
            result.sosync_write_date = GetPersonSosyncWriteDate(result);

            return result;
        }
        

        private void SetdboPersonStack_fso_ids(
            dboPersonStack person,
            int onlineID,
            DataService<dboPerson> personSvc,
            DataService<dboPersonAdresse> addressSvc,
            DataService<dboPersonEmail> emailSvc,
            DataService<dboPersonTelefon> phoneSvc,
            DataService<dboPersonTelefon> mobileSvc,
            DataService<dboPersonTelefon> faxSvc,
            DataService<dboPersonGruppe> personDonationDeductionOptOutSvc,
            DataService<dboPersonGruppe> personDonationReceiptSvc,
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

            var synced_dboPersonTelefonIDS = new List<int>();
            if (person.phone != null)
            {
                synced_dboPersonTelefonIDS.Add(person.phone.PersonTelefonID);
            }

            if (person.mobile != null)
            {
                synced_dboPersonTelefonIDS.Add(person.mobile.PersonTelefonID);
            }

            if (person.fax != null)
            {
                synced_dboPersonTelefonIDS.Add(person.fax.PersonTelefonID);
            }

            foreach (dboPersonTelefon iterPhone in phoneSvc.Read(new { PersonID = person.person.PersonID }))
            {
                if (person.phone == null && person.mobile == null && person.fax == null)
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
                    if ( synced_dboPersonTelefonIDS.Contains(iterPhone.PersonTelefonID) )
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

            if (person.personDonationReceipt != null)
            {
                if ((person.personDonationReceipt.sosync_fso_id ?? 0) != onlineID)
                {
                    person.personDonationReceipt.sosync_fso_id = onlineID;
                    person.personDonationReceipt.noSyncJobSwitch = true;
                    personDonationReceiptSvc.Update(person.personDonationReceipt);
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

            var sosync_write_date = (person.person.sosync_write_date ?? person.person.write_date);

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

                if (person.address.AnredeformtypID == 324)
                {
                    data.Add("anrede_individuell", person.address.IndividuelleAnrede);
                }
                else
                {
                    data.Add("anrede_individuell", null);
                }

                string countryCode = null;

                using(var dbSvc = MdbService.GetDataService<dboTypen>())
                {
                    countryCode = dbSvc.ExecuteQuery<string>("select sosync.LandID_to_IsoCountryCode2(@LandID)", new { LandID = person.address.LandID }).FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(countryCode))
                {

                    var foundCountryID = OdooService.Client.SearchModelByField<resCountry, string>("res.country", x => x.Code, countryCode).FirstOrDefault();

                    if(foundCountryID != 0)
                    {
                        data.Add("country_id", foundCountryID);
                    }
                    else
                    {
                        data.Add("country_id", null);
                    }

                }
                else
                {
                    data.Add("country_id", null);
                }


            }
            else
            {
                data.Add("street", null);
                data.Add("street_number_web", null);
                data.Add("zip", null);
                data.Add("city", null);
                data.Add("anrede_individuell", null);
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

            if (person.mobile != null)
            {
                data.Add("mobile", (person.mobile.Landkennzeichen + " " + person.mobile.Vorwahl + " " + person.mobile.Rufnummer).Trim());
            }
            else
            {
                data.Add("mobile", null);
            }

            if (person.fax != null)
            {
                data.Add("fax", (person.fax.Landkennzeichen + " " + person.fax.Vorwahl + " " + person.fax.Rufnummer).Trim());
            }
            else
            {
                data.Add("fax", null);
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
            else
            {
                data.Add("donation_deduction_optout_web", false);
            }

            //donation_receipt_web
            if (person.personDonationReceipt != null)
            {
                if (person.personDonationReceipt.GültigVon <= DateTime.Today
                    && person.personDonationReceipt.GültigBis >= DateTime.Today)
                {
                    data.Add("donation_receipt_web", true);
                }
                else
                {
                    data.Add("donation_receipt_web", false);
                }
            }
            else
            {
                data.Add("donation_receipt_web", false);
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
            else
            {
                data.Add("newsletter_web", false);
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
                using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>())
                using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>())
                using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
                using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>())
                using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
                {
                    SetdboPersonStack_fso_ids(
                        person, 
                        odooPartnerId, 
                        personSvc, 
                        addressSvc, 
                        emailSvc, 
                        phoneSvc, 
                        mobileSvc,
                        faxSvc,
                        personDonationDeductionOptOutSvc, 
                        personDonationReceiptSvc,
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

                    var onlineID = person.person.sosync_fso_id.Value;

                    // Update res.partner
                    OdooService.Client.UpdateModel("res.partner", data, onlineID, false);

                    // Update the remote id in studio
                    using (var personSvc = MdbService.GetDataService<dboPerson>())
                    using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
                    using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
                    using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
                    using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>())
                    using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>())
                    using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>())
                    using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>())
                    using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
                    {
                        SetdboPersonStack_fso_ids(
                            person,
                            onlineID,
                            personSvc,
                            addressSvc,
                            emailSvc,
                            phoneSvc,
                            mobileSvc,
                            faxSvc,
                            personDonationDeductionOptOutSvc,
                            personDonationReceiptSvc,
                            emailNewsletterSvc,
                            null,
                            null);
                    }
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
                using (var mobileSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
                using (var faxSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
                using (var personDonationDeductionOptOutSvc = MdbService.GetDataService<dboPersonGruppe>(con, transaction))
                using (var personDonationReceiptSvc = MdbService.GetDataService<dboPersonGruppe>(con, transaction))
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
                            person.phone = InitDboPersonTelefon(dboPersonTelefontyp.phone);

                            person.phone.sosync_fso_id = onlineID;

                            person.phone.sosync_write_date = sosync_write_date;
                            person.phone.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefon(partner, person.phone);

                        }

                        //create new dboPersonTelefon if mobile is filled
                        if (PartnerHasMobile(partner))
                        {
                            person.mobile = InitDboPersonTelefon(dboPersonTelefontyp.mobile);

                            person.mobile.sosync_fso_id = onlineID;

                            person.mobile.sosync_write_date = sosync_write_date;
                            person.mobile.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefonMobil(partner, person.mobile);

                        }

                        //create new dboPersonTelefon if phone is filled
                        if (PartnerHasFax(partner))
                        {
                            person.fax = InitDboPersonTelefon(dboPersonTelefontyp.fax);

                            person.fax.sosync_fso_id = onlineID;

                            person.fax.sosync_write_date = sosync_write_date;
                            person.fax.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefonFax(partner, person.fax);

                        }

                        //create new dboPersonGruppe if don-de-oo
                        if (partner.DonationDeductionOptOut ?? false)
                        {
                            person.personDonationDeductionOptOut = InitPersonDonationDeductionOptOut();

                            person.personDonationDeductionOptOut.sosync_fso_id = onlineID;

                            person.personDonationDeductionOptOut.sosync_write_date = sosync_write_date;
                            person.personDonationDeductionOptOut.noSyncJobSwitch = true;

                        }

                        //create new dboPersonGruppe if don-rec
                        if (partner.DonationReceipt ?? false)
                        {
                            person.personDonationReceipt = InitPersonDonationReceipt();

                            person.personDonationReceipt.sosync_fso_id = onlineID;

                            person.personDonationReceipt.sosync_write_date = sosync_write_date;
                            person.personDonationReceipt.noSyncJobSwitch = true;

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

                            if (person.mobile != null)
                            {
                                person.mobile.PersonID = PersonID;
                                mobileSvc.Create(person.mobile);
                            }

                            if (person.fax != null)
                            {
                                person.fax.PersonID = PersonID;
                                faxSvc.Create(person.fax);
                            }

                            if (person.personDonationDeductionOptOut != null)
                            {
                                person.personDonationDeductionOptOut.PersonID = PersonID;
                                personDonationDeductionOptOutSvc.Create(person.personDonationDeductionOptOut);
                            }


                            if(person.personDonationReceipt != null)
                            {
                                person.personDonationReceipt.PersonID = PersonID;
                                personDonationReceiptSvc.Create(person.personDonationReceipt);
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
                                person.phone = InitDboPersonTelefon(dboPersonTelefontyp.phone);

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

                            person.phone.sosync_fso_id = onlineID;
                            person.phone.sosync_write_date = sosync_write_date;
                            person.phone.noSyncJobSwitch = true;

                        }

                        if (person.mobile == null)
                        {
                            if (PartnerHasMobile(partner))
                            {
                                person.mobile = InitDboPersonTelefon(dboPersonTelefontyp.mobile);

                                person.mobile.PersonID = PersonID;

                                CopyPartnerToPersonTelefonMobil(partner, person.mobile);

                                person.mobile.sosync_fso_id = onlineID;
                                person.mobile.sosync_write_date = sosync_write_date;
                                person.mobile.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {

                            CopyPartnerToPersonTelefonMobil(partner, person.mobile);

                            person.mobile.sosync_fso_id = onlineID;
                            person.mobile.sosync_write_date = sosync_write_date;
                            person.mobile.noSyncJobSwitch = true;

                        }

                        if (person.fax == null)
                        {
                            if (PartnerHasFax(partner))
                            {
                                person.fax = InitDboPersonTelefon(dboPersonTelefontyp.fax);

                                person.fax.PersonID = PersonID;

                                CopyPartnerToPersonTelefonMobil(partner, person.fax);

                                person.fax.sosync_fso_id = onlineID;
                                person.fax.sosync_write_date = sosync_write_date;
                                person.fax.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {

                            CopyPartnerToPersonTelefonMobil(partner, person.fax);

                            person.fax.sosync_fso_id = onlineID;
                            person.fax.sosync_write_date = sosync_write_date;
                            person.fax.noSyncJobSwitch = true;

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

                        if (person.personDonationReceipt == null)
                        {
                            if (partner.DonationReceipt ?? false)
                            {
                                person.personDonationReceipt = InitPersonDonationReceipt();

                                person.personDonationReceipt.PersonID = PersonID;

                                person.personDonationReceipt.sosync_fso_id = onlineID;
                                person.personDonationReceipt.sosync_write_date = sosync_write_date;
                                person.personDonationReceipt.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            if (partner.DonationReceipt ?? false)
                            {
                                if (person.personDonationReceipt.GültigVon > DateTime.Today)
                                {
                                    person.personDonationReceipt.GültigVon = DateTime.Today;
                                }
                                person.personDonationReceipt.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.personDonationReceipt.GültigVon > DateTime.Today)
                                {
                                    person.personDonationReceipt.GültigVon = DateTime.Today;
                                }
                                person.personDonationReceipt.GültigBis = DateTime.Today;
                            }

                            person.personDonationReceipt.sosync_fso_id = onlineID;
                            person.personDonationReceipt.sosync_write_date = sosync_write_date;
                            person.personDonationReceipt.noSyncJobSwitch = true;
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

                            if (person.mobile != null)
                            {
                                if (person.mobile.PersonTelefonID == 0)
                                {
                                    mobileSvc.Create(person.mobile);
                                }
                                else
                                {
                                    mobileSvc.Update(person.mobile);
                                }
                            }

                            if (person.fax != null)
                            {
                                if (person.fax.PersonTelefonID == 0)
                                {
                                    faxSvc.Create(person.fax);
                                }
                                else
                                {
                                    faxSvc.Update(person.fax);
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
                               mobileSvc,
                               faxSvc,
                               personDonationDeductionOptOutSvc,
                               personDonationReceiptSvc,
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
            dest.BPKErzwungenPLZ = source.BPKForcedZip;
        }
        private void CopyPartnerToPersonAddress(resPartner source, dboPersonAdresse dest)
        {



            if (source.CountryID.HasValue)
            {
                var country = OdooService.Client.GetModel<resCountry>("res.country", source.CountryID.Value);
                if (country != null)
                {

                    using (var dbSvc = MdbService.GetDataService<dboTypen>())
                    {
                        var foundLandID = dbSvc.ExecuteQuery<int?>("select sosync.IsoCountryCode2_to_LandID(@Code)", new { Code = country.Code }).FirstOrDefault();

                        if (foundLandID.HasValue && foundLandID.Value != 0)
                        {
                            dest.LandID = foundLandID.Value;
                        }

                    }
                }
            }


            if (!string.IsNullOrEmpty(source.AnredeIndividuell))
            {
                dest.AnredeformtypID = 324; //individuell
                dest.IndividuelleAnrede = source.AnredeIndividuell;
            }
            else if (dest.AnredeformtypID == 324)
            {

                int system_default_AnredeformtypID = 0;
                using (var typenSVC = MdbService.GetDataService<dboTypen>())
                {
                    system_default_AnredeformtypID = (int)typenSVC.Read(new { TypenID = 200120 }).FirstOrDefault().Formularwert;
                }

                dest.AnredeformtypID = system_default_AnredeformtypID;

            }

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

        private void CopyPartnerToPersonTelefonMobil(resPartner source, dboPersonTelefon dest)
        {
            //TODO: parse phone into its parts
            dest.Landkennzeichen = "";
            dest.Vorwahl = "";
            dest.Rufnummer = source.Mobile;


        }

        private void CopyPartnerToPersonTelefonFax(resPartner source, dboPersonTelefon dest)
        {
            //TODO: parse phone into its parts
            dest.Landkennzeichen = "";
            dest.Vorwahl = "";
            dest.Rufnummer = source.Fax;


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

        private dboPersonTelefon InitDboPersonTelefon(dboPersonTelefontyp phoneType)
        {
            dboPersonTelefon result = null;


            result = new dboPersonTelefon
            {
                TelefontypID = (int)phoneType,
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
        private dboPersonGruppe InitPersonDonationReceipt()
        {
            dboPersonGruppe result = null;


            result = new dboPersonGruppe
            {
                zGruppeDetailID = 20168,
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

        private bool PartnerHasMobile(resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Mobile));
        }

        private bool PartnerHasFax(resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Fax));
        }

        #endregion
    }
}
