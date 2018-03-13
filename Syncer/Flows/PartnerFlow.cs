﻿using dadi_data;
using dadi_data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Odoo;
using Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    public class PartnerFlow : ReplicateSyncFlow
    {

        #region Members
        private ILogger<PartnerFlow> _log;
        #endregion

        #region Constructors
        public PartnerFlow(IServiceProvider svc, SosyncOptions conf)
            : base(svc, conf)
        {
            _log = (ILogger<PartnerFlow>)svc.GetService(typeof(ILogger<PartnerFlow>));
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "res.partner");

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
            {
                // Since we're only running a simple query, the DataService type doesn't matter
                using (var db = MdbService.GetDataService<dboPerson>())
                {
                    var foundStudioID = db.ExecuteQuery<int?>(
                        $"select PersonID from dbo.Person where sosync_fso_id = @fso_id",
                        new { fso_id = onlineID })
                        .SingleOrDefault();

                    if (foundStudioID.HasValue)
                        info.ForeignID = foundStudioID;
                }
            }

            return info;
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
                person.personDonationReceipt != null ? person.personDonationReceipt.write_date : (DateTime?)null,
                person.SystemDeactivateBPK != null ? person.SystemDeactivateBPK.write_date : (DateTime?)null
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
                person.personDonationReceipt != null ? person.personDonationReceipt.sosync_write_date : (DateTime?)null,
                person.SystemDeactivateBPK != null ? person.SystemDeactivateBPK.sosync_write_date : (DateTime?)null
            }.Where(x => x.HasValue);

            if (query.Any())
                return query.Max();

            return null;
        }

        private dboPersonStack GetCurrentdboPersonStack(int PersonID)
        {
            LogMilliseconds($"{nameof(GetCurrentdboPersonStack)} start", 0);
            Stopwatch s = new Stopwatch();
            s.Start();

            dboPersonStack result = new dboPersonStack();

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
            using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>())
            using (var emailSvc = MdbService.GetDataService<dboPersonEmail>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            using (var personGroupSvc = MdbService.GetDataService<dboPersonGruppe>())
            using (var emailGroupSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
            {
                result.person = personSvc.Read(new { PersonID = PersonID }).FirstOrDefault();

                if (!result.person.sosync_fso_id.HasValue)
                    result.person.sosync_fso_id = GetFsoIdByFsId("res.partner", result.person.PersonID);

                result.address = (from iterAddress in addressSvc.Read(new { PersonID = PersonID })
                                  where iterAddress.GültigVon <= DateTime.Today &&
                                      iterAddress.GültigBis >= DateTime.Today
                                  orderby string.IsNullOrEmpty(iterAddress.GültigMonatArray) ? "111111111111" : iterAddress.GültigMonatArray descending,
                                  iterAddress.PersonAdresseID descending
                                  select iterAddress).FirstOrDefault();
              
                if (result.address != null)
                    result.addressAM = addressAMSvc.Read(new { PersonAdresseID = result.address.PersonAdresseID }).FirstOrDefault();

                result.phone = (from iterPhone in phoneSvc.Read(new { PersonID = PersonID })
                                where iterPhone.GültigVon <= DateTime.Today &&
                                    iterPhone.GültigBis >= DateTime.Today &&
                                    iterPhone.TelefontypID == 400
                                orderby string.IsNullOrEmpty(iterPhone.GültigMonatArray) ? "111111111111" : iterPhone.GültigMonatArray descending,
                                iterPhone.PersonTelefonID descending
                                select iterPhone).FirstOrDefault();

                result.mobile = (from itermobile in phoneSvc.Read(new { PersonID = PersonID })
                                where itermobile.GültigVon <= DateTime.Today &&
                                    itermobile.GültigBis >= DateTime.Today &&
                                    itermobile.TelefontypID == 401
                                orderby string.IsNullOrEmpty(itermobile.GültigMonatArray) ? "111111111111" : itermobile.GültigMonatArray descending,
                                itermobile.PersonTelefonID descending
                                 select itermobile).FirstOrDefault();

                result.fax = (from iterfax in phoneSvc.Read(new { PersonID = PersonID })
                                where iterfax.GültigVon <= DateTime.Today &&
                                    iterfax.GültigBis >= DateTime.Today &&
                                    iterfax.TelefontypID == 403
                                orderby string.IsNullOrEmpty(iterfax.GültigMonatArray) ? "111111111111" : iterfax.GültigMonatArray descending,
                                iterfax.PersonTelefonID descending
                              select iterfax).FirstOrDefault();

                result.email = (from iterEmail in emailSvc.Read(new { PersonID = PersonID })
                                where iterEmail.GültigVon <= DateTime.Today &&
                                    iterEmail.GültigBis >= DateTime.Today
                                orderby string.IsNullOrEmpty(iterEmail.GültigMonatArray) ? "111111111111" : iterEmail.GültigMonatArray descending,
                                iterEmail.PersonEmailID descending
                                select iterEmail).FirstOrDefault();

                result.personDonationDeductionOptOut = personGroupSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 110493 }).FirstOrDefault();

                result.personDonationReceipt = personGroupSvc.Read(new { PersonID = PersonID, zGruppeDetailID = 20168 }).FirstOrDefault();


                if (result.email != null)
                    result.emailNewsletter = emailGroupSvc.Read(
                        new { PersonEmailID = result.email.PersonEmailID, zGruppeDetailID = 30104 })
                        .FirstOrDefault();

                // Select the first active "bPK deactivated by system" group
                result.SystemDeactivateBPK = personGroupSvc.Read(
                    new { PersonID = PersonID, zGruppeDetailID = 128782 })
                    .FirstOrDefault();
            }

            result.write_date = GetPersonWriteDate(result);
            result.sosync_write_date = GetPersonSosyncWriteDate(result);

            s.Stop();
            LogMs(0, $"{nameof(GetCurrentdboPersonStack)} done", null, s.ElapsedMilliseconds);

            return result;
        }
        

        private void SetdboPersonStack_fso_ids(
            dboPersonStack person,
            int onlineID,
            DataService<dboPerson> personSvc,
            DataService<dboPersonAdresse> addressSvc,
            DataService<dboPersonEmail> emailSvc,
            DataService<dboPersonTelefon> phoneSvc,
            DataService<dboPersonGruppe> personGroupSvc,
            DataService<dboPersonEmailGruppe> emailNewsletterSvc,
            SqlConnection con,
            SqlTransaction transaction)
        {
            LogMilliseconds($"{nameof(SetdboPersonStack_fso_ids)} start", 0);
            var s = new Stopwatch();
            s.Start();

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
                synced_dboPersonTelefonIDS.Add(person.phone.PersonTelefonID);

            if (person.mobile != null)
                synced_dboPersonTelefonIDS.Add(person.mobile.PersonTelefonID);

            if (person.fax != null)
                synced_dboPersonTelefonIDS.Add(person.fax.PersonTelefonID);

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
                    personGroupSvc.Update(person.personDonationDeductionOptOut);
                }
            }

            if (person.personDonationReceipt != null)
            {
                if ((person.personDonationReceipt.sosync_fso_id ?? 0) != onlineID)
                {
                    person.personDonationReceipt.sosync_fso_id = onlineID;
                    person.personDonationReceipt.noSyncJobSwitch = true;
                    personGroupSvc.Update(person.personDonationReceipt);
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

            if (person.SystemDeactivateBPK != null)
            {
                if ((person.SystemDeactivateBPK.sosync_fso_id ?? 0) != onlineID)
                {
                    person.SystemDeactivateBPK.sosync_fso_id = onlineID;
                    person.SystemDeactivateBPK.noSyncJobSwitch = true;
                    personGroupSvc.Update(person.SystemDeactivateBPK);
                }
            }
            s.Stop();
            LogMs(1, $"{nameof(SetdboPersonStack_fso_ids)} done", Job.Job_ID, Convert.ToInt64(s.Elapsed.TotalMilliseconds));
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

            var sosync_write_date = (person.sosync_write_date ?? person.write_date);

            var data = new Dictionary<string, object>()
                {
                    { "firstname", person.person.Vorname },
                    { "lastname", person.person.Name },
                    { "name_zwei", person.person.Name2 },
                    { "birthdate_web", OdooConvert.ToStringOrBoolFalse(person.person.Geburtsdatum) },
                    { "title_web", person.person.Titel },
                    { "bpk_forced_firstname", person.person.BPKErzwungenVorname },
                    { "bpk_forced_lastname", person.person.BPKErzwungenNachname },
                    { "bpk_forced_birthdate", person.person.BPKErzwungenGeburtsdatum },
                    { "bpk_forced_zip", person.person.BPKErzwungenPLZ },
                    { "sosync_write_date", sosync_write_date }
                    // Do NOT sync bpk_state back!
                };

            if (new int[] { 290, 291 }.Contains(person.person.GeschlechttypID))
            {
                if (person.person.GeschlechttypID == 290)
                    data.Add("gender", "male");
                else
                    data.Add("gender", "female");
            }
            else
            {
                data.Add("gender", "other");
            }
                

            if (person.address != null)
            {
                data.Add("street", person.address.Strasse);
                data.Add("street_number_web", person.address.Hausnummer);
                data.Add("zip", person.address.PLZ);
                data.Add("city", person.address.Ort);

                if (person.address.AnredeformtypID == 324)
                    data.Add("anrede_individuell", person.address.IndividuelleAnrede);
                else
                    data.Add("anrede_individuell", null);

                string countryCode = null;

                using(var dbSvc = MdbService.GetDataService<dboTypen>())
                {
                    countryCode = dbSvc.ExecuteQuery<string>(
                        "select sosync.LandID_to_IsoCountryCode2(@LandID)",
                        new { LandID = person.address.LandID })
                        .FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(countryCode))
                {

                    var foundCountryID = (int?)OdooService.Client.SearchModelByField<resCountry, string>(
                        "res.country",
                        x => x.Code,
                        countryCode)
                        .FirstOrDefault();

                    if(foundCountryID != 0)
                        data.Add("country_id", foundCountryID);
                    else
                        data.Add("country_id", null);

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
                data.Add("email", person.email.EmailVor + "@" + person.email.EmailNach);
            else
                data.Add("email", null);

            if (person.phone != null)
                data.Add("phone", CombinePhone(person.phone.Landkennzeichen, person.phone.Vorwahl, person.phone.Rufnummer));
            else
                data.Add("phone", null);

            if (person.mobile != null)
                data.Add("mobile", CombinePhone(person.mobile.Landkennzeichen, person.mobile.Vorwahl, person.mobile.Rufnummer));
            else
                data.Add("mobile", null);

            if (person.fax != null)
                data.Add("fax", CombinePhone(person.fax.Landkennzeichen, person.fax.Vorwahl, person.fax.Rufnummer));
            else
                data.Add("fax", null);

            if (person.personDonationDeductionOptOut != null)
            {
                if (person.personDonationDeductionOptOut.GültigVon <= DateTime.Today
                    && person.personDonationDeductionOptOut.GültigBis >= DateTime.Today)
                    data.Add("donation_deduction_optout_web", true);
                else
                    data.Add("donation_deduction_optout_web", false);
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
                    data.Add("donation_receipt_web", true);
                else
                    data.Add("donation_receipt_web", false);
            }
            else
            {
                data.Add("donation_receipt_web", false);
            }

            if (person.emailNewsletter != null)
            {
                if (person.emailNewsletter.GültigVon <= DateTime.Today
                    && person.emailNewsletter.GültigBis >= DateTime.Today)
                    data.Add("newsletter_web", true);
                else
                    data.Add("newsletter_web", false);
            }
            else
            {
                data.Add("newsletter_web", false);
            }

            if (person.SystemDeactivateBPK != null)
            {
                if (person.SystemDeactivateBPK.GültigVon <= DateTime.Today
                    && person.SystemDeactivateBPK.GültigBis >= DateTime.Today)
                    data.Add("donation_deduction_disabled", true);
                else
                    data.Add("donation_deduction_disabled", false);
            }
            else
            {
                data.Add("donation_deduction_disabled", false);
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
                using (var personGroupSvc = MdbService.GetDataService<dboPersonGruppe>())
                using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
                {
                    SetdboPersonStack_fso_ids(
                        person, 
                        odooPartnerId, 
                        personSvc, 
                        addressSvc, 
                        emailSvc, 
                        phoneSvc, 
                        personGroupSvc,
                        emailNewsletterSvc, 
                        null, 
                        null);
                }
            }
            else
            {
                OdooService.Client.GetModel<resPartner>("res.partner", person.person.sosync_fso_id.Value);
                LogMilliseconds($"{nameof(TransformToOnline)} read full res.partner", OdooService.Client.LastRpcTime);

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
                    using (var personGroupSvc = MdbService.GetDataService<dboPersonGruppe>())
                    using (var emailNewsletterSvc = MdbService.GetDataService<dboPersonEmailGruppe>())
                    {
                        SetdboPersonStack_fso_ids(
                            person,
                            onlineID,
                            personSvc,
                            addressSvc,
                            emailSvc,
                            phoneSvc,
                            personGroupSvc,
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

        private string CombinePhone(string countryCodeWithZeros, string areaCodeWithZero, string number)
        {
            var country = (countryCodeWithZeros ?? "").Trim();
            var area = (areaCodeWithZero ?? "").Trim();

            if (area.StartsWith("0"))
                area = area.Substring(1);

            return $"{country} {area} {number}".Trim();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var partner = OdooService.Client.GetModel<resPartner>("res.partner", onlineID);
            LogMilliseconds($"{nameof(TransformToStudio)} read res.partner", OdooService.Client.LastRpcTime);           

            var sosync_write_date = (partner.Sosync_Write_Date ?? partner.Write_Date).Value;

            if (!IsValidFsID(partner.Sosync_FS_ID))
                partner.Sosync_FS_ID = GetFsIdByFsoId("dbo.Person", "PersonID", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            {
                var transaction = personSvc.BeginTransaction();
                var con = personSvc.Connection;

                using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>(con, transaction))
                using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>(con, transaction))
                using (var emailSvc = MdbService.GetDataService<dboPersonEmail>(con, transaction))
                using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
                using (var personGroupSvc = MdbService.GetDataService<dboPersonGruppe>(con, transaction))
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

                            CopyPartnerToPersonTelefon(partner, person.phone, phoneSvc);
                        }

                        //create new dboPersonTelefon if mobile is filled
                        if (PartnerHasMobile(partner))
                        {
                            person.mobile = InitDboPersonTelefon(dboPersonTelefontyp.mobile);

                            person.mobile.sosync_fso_id = onlineID;
                            person.mobile.sosync_write_date = sosync_write_date;
                            person.mobile.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefonMobil(partner, person.mobile, phoneSvc);
                        }

                        //create new dboPersonTelefon if phone is filled
                        if (PartnerHasFax(partner))
                        {
                            person.fax = InitDboPersonTelefon(dboPersonTelefontyp.fax);

                            person.fax.sosync_fso_id = onlineID;
                            person.fax.sosync_write_date = sosync_write_date;
                            person.fax.noSyncJobSwitch = true;

                            CopyPartnerToPersonTelefonFax(partner, person.fax, phoneSvc);
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

                        if (partner.DonationDeductionDisabled ?? false)
                        {
                            person.SystemDeactivateBPK = InitSystemDeactivateBPK();
                            person.SystemDeactivateBPK.sosync_fso_id = onlineID;
                            person.SystemDeactivateBPK.sosync_write_date = sosync_write_date;
                            person.SystemDeactivateBPK.noSyncJobSwitch = true;
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
                                phoneSvc.Create(person.mobile);
                            }

                            if (person.fax != null)
                            {
                                person.fax.PersonID = PersonID;
                                phoneSvc.Create(person.fax);
                            }

                            if (person.personDonationDeductionOptOut != null)
                            {
                                person.personDonationDeductionOptOut.PersonID = PersonID;
                                personGroupSvc.Create(person.personDonationDeductionOptOut);
                            }

                            if(person.personDonationReceipt != null)
                            {
                                person.personDonationReceipt.PersonID = PersonID;
                                personGroupSvc.Create(person.personDonationReceipt);
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
                        LogMilliseconds($"{nameof(TransformToStudio)} update res.partner", OdooService.Client.LastRpcTime);
                    }
                    else
                    {
                        var PersonID = partner.Sosync_FS_ID.Value;
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

                                CopyPartnerToPersonTelefon(partner, person.phone, phoneSvc);

                                person.phone.sosync_fso_id = onlineID;
                                person.phone.sosync_write_date = sosync_write_date;
                                person.phone.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            CopyPartnerToPersonTelefon(partner, person.phone, phoneSvc);

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

                                CopyPartnerToPersonTelefonMobil(partner, person.mobile, phoneSvc);

                                person.mobile.sosync_fso_id = onlineID;
                                person.mobile.sosync_write_date = sosync_write_date;
                                person.mobile.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            CopyPartnerToPersonTelefonMobil(partner, person.mobile, phoneSvc);

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

                                CopyPartnerToPersonTelefonMobil(partner, person.fax, phoneSvc);

                                person.fax.sosync_fso_id = onlineID;
                                person.fax.sosync_write_date = sosync_write_date;
                                person.fax.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            CopyPartnerToPersonTelefonMobil(partner, person.fax, phoneSvc);

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
                                    person.personDonationDeductionOptOut.GültigVon = DateTime.Today;

                                person.personDonationDeductionOptOut.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.personDonationDeductionOptOut.GültigVon > DateTime.Today)
                                    person.personDonationDeductionOptOut.GültigVon = DateTime.Today;

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
                                    person.personDonationReceipt.GültigVon = DateTime.Today;

                                person.personDonationReceipt.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.personDonationReceipt.GültigVon > DateTime.Today)
                                    person.personDonationReceipt.GültigVon = DateTime.Today;

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
                                    person.emailNewsletter.GültigVon = DateTime.Today;

                                person.emailNewsletter.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.emailNewsletter.GültigVon > DateTime.Today)
                                    person.emailNewsletter.GültigVon = DateTime.Today;

                                person.emailNewsletter.GültigBis = DateTime.Today;
                            }

                            person.emailNewsletter.sosync_fso_id = onlineID;
                            person.emailNewsletter.sosync_write_date = sosync_write_date;
                            person.emailNewsletter.noSyncJobSwitch = true;
                        }

                        if (person.SystemDeactivateBPK == null)
                        {
                            if (partner.DonationDeductionDisabled.HasValue && partner.DonationDeductionDisabled == true)
                            {
                                person.SystemDeactivateBPK = InitSystemDeactivateBPK();

                                person.SystemDeactivateBPK.PersonID = person.person.PersonID;

                                person.SystemDeactivateBPK.sosync_fso_id = onlineID;
                                person.SystemDeactivateBPK.sosync_write_date = sosync_write_date;
                                person.SystemDeactivateBPK.noSyncJobSwitch = true;
                            }
                        }
                        else
                        {
                            if (partner.DonationDeductionDisabled ?? false == true)
                            {
                                if (person.SystemDeactivateBPK.GültigVon > DateTime.Today)
                                    person.SystemDeactivateBPK.GültigVon = DateTime.Today;

                                person.SystemDeactivateBPK.GültigBis = new DateTime(2099, 12, 31);
                            }
                            else
                            {
                                if (person.SystemDeactivateBPK.GültigVon > DateTime.Today)
                                    person.SystemDeactivateBPK.GültigVon = DateTime.Today;

                                person.SystemDeactivateBPK.GültigBis = DateTime.Today;
                            }
                        }

                        UpdateSyncTargetRequest(Serializer.ToXML(person));

                        try
                        {
                            personSvc.Update(person.person);
                            LogMilliseconds($"{nameof(TransformToStudio)} update dbo.Person", personSvc.LastQueryExecutionTimeMS);

                            if (person.address != null)
                            {
                                if (person.address.PersonAdresseID == 0)
                                {
                                    addressSvc.Create(person.address);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonAdresse", addressSvc.LastQueryExecutionTimeMS);

                                    person.addressAM.PersonAdresseID = person.address.PersonAdresseID;

                                    addressAMSvc.Create(person.addressAM);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonAdresseAM", addressAMSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    addressSvc.Update(person.address);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonAdresse", addressSvc.LastQueryExecutionTimeMS);

                                    addressAMSvc.Update(person.addressAM);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonAdresseAM", addressSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.email != null)
                            {
                                if (person.email.PersonEmailID == 0)
                                {
                                    emailSvc.Create(person.email);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonEmail", emailSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    emailSvc.Update(person.email);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonEmail", emailSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.phone != null)
                            {
                                if (person.phone.PersonTelefonID == 0)
                                {
                                    phoneSvc.Create(person.phone);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonTelefon (Festnetz)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    phoneSvc.Update(person.phone);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonTelefon (Festnetz)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.mobile != null)
                            {
                                if (person.mobile.PersonTelefonID == 0)
                                {
                                    phoneSvc.Create(person.mobile);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonTelefon (Mobil)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    phoneSvc.Update(person.mobile);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonTelefon (Mobil)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.fax != null)
                            {
                                if (person.fax.PersonTelefonID == 0)
                                {
                                    phoneSvc.Create(person.fax);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonTelefon (Fax)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    phoneSvc.Update(person.fax);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonTelefon (Fax)", phoneSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.personDonationDeductionOptOut != null)
                            {
                                if (person.personDonationDeductionOptOut.PersonGruppeID == 0)
                                {
                                    personGroupSvc.Create(person.personDonationDeductionOptOut);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonGruppe (DonationDeductionOptOut)", personGroupSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    personGroupSvc.Update(person.personDonationDeductionOptOut);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonGruppe (DonationDeductionOptOut)", personGroupSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.emailNewsletter != null)
                            {
                                if (person.emailNewsletter.PersonEmailGruppeID == 0)
                                {
                                    emailNewsletterSvc.Create(person.emailNewsletter);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonEmailGruppe", emailNewsletterSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    emailNewsletterSvc.Update(person.emailNewsletter);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonEmailGruppe", emailNewsletterSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            if (person.SystemDeactivateBPK != null)
                            {
                                if (person.SystemDeactivateBPK.PersonGruppeID == 0)
                                {
                                    personGroupSvc.Create(person.SystemDeactivateBPK);
                                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonGruppe (DeactivateBPK)", personGroupSvc.LastQueryExecutionTimeMS);
                                }
                                else
                                {
                                    personGroupSvc.Update(person.SystemDeactivateBPK);
                                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonGruppe (DeactivateBPK)", personGroupSvc.LastQueryExecutionTimeMS);
                                }
                            }

                            SetdboPersonStack_fso_ids(
                               person,
                               onlineID,
                               personSvc,
                               addressSvc,
                               emailSvc,
                               phoneSvc,
                               personGroupSvc,
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
                    LogMilliseconds($"{nameof(TransformToStudio)} commit transaction dbo.Person", personSvc.LastQueryExecutionTimeMS);
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
            dest.fso_bpk_state = source.BPKState;

            if (new string[] { "male", "female" }.Contains(source.Gender))
                dest.GeschlechttypID = source.Gender == "male" ? 290 : 291;
            else if (source.Gender == "other" && !new int[] { 292, 293, 294, 295 }.Contains(dest.GeschlechttypID))
                dest.GeschlechttypID = 0;
            else if (string.IsNullOrEmpty(source.Gender))
                dest.GeschlechttypID = 0;

        }
        private void CopyPartnerToPersonAddress(resPartner source, dboPersonAdresse dest)
        {
            if (source.CountryID != null && source.CountryID.Length > 0)
            {
                var country = OdooService.Client.GetModel<resCountry>("res.country", Convert.ToInt32(source.CountryID[0]));
                LogMilliseconds($"{nameof(CopyPartnerToPersonAddress)} read res.country", OdooService.Client.LastRpcTime);

                if (country != null)
                {
                    using (var dbSvc = MdbService.GetDataService<dboTypen>())
                    {
                        var foundLandID = dbSvc.ExecuteQuery<int?>("select sosync.IsoCountryCode2_to_LandID(@Code)", new { Code = country.Code }).FirstOrDefault();
                        LogMilliseconds($"{nameof(CopyPartnerToPersonAddress)} get LandID for ISO Code", dbSvc.LastQueryExecutionTimeMS);

                        if (foundLandID.HasValue && foundLandID.Value != 0)
                            dest.LandID = foundLandID.Value;
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
                    LogMilliseconds($"{nameof(CopyPartnerToPersonAddress)} get Type<200120>", typenSVC.LastQueryExecutionTimeMS);
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

        private PhoneCorrection QueryPhoneCorrection(string number, DataService<dboPersonTelefon> db)
        {
            var parameters = new DynamicParameters();
            parameters.Add("Telefonnummer", number, size: 200);
            parameters.Add("Telefonlandkennzeichen", direction: System.Data.ParameterDirection.Output, size: 10);
            parameters.Add("Telefonvorwahl", direction: System.Data.ParameterDirection.Output, size: 50);
            parameters.Add("Telefonrufnummer", direction: System.Data.ParameterDirection.Output, size: 50);
            parameters.Add("TelefontypID", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
            parameters.Add("TelefonLandname", direction: System.Data.ParameterDirection.Output, size: 100);
            parameters.Add("ReturnValue", direction: System.Data.ParameterDirection.Output, size: 200);

            db.ExecuteProcedure(
                "[dbo].[stp_860_WebService_06000_sysbst_820_TelefonAufbereiten]",
                parameters);

            var result = new PhoneCorrection()
            {
                Telefonlandkennzeichen = parameters.Get<string>("Telefonlandkennzeichen"),
                Telefonvorwahl = parameters.Get<string>("Telefonvorwahl"),
                Telefonrufnummer = parameters.Get<string>("Telefonrufnummer"),
                TelefontypID = parameters.Get<int>("TelefontypID"),
                TelefonLandname = parameters.Get<string>("TelefonLandname")
            };

            return result;
        }

        private void CopyPartnerToPersonTelefon(resPartner source, dboPersonTelefon dest, DataService<dboPersonTelefon> db)
        {
#warning TODO: parse phone into its parts
            dest.RufnummerGesamt = source.Phone;

            var p = QueryPhoneCorrection(source.Phone, db);

            dest.Landkennzeichen = p.Telefonlandkennzeichen;
            dest.Vorwahl = p.Telefonvorwahl;
            dest.Rufnummer = p.Telefonrufnummer;
        }

        private void CopyPartnerToPersonTelefonMobil(resPartner source, dboPersonTelefon dest, DataService<dboPersonTelefon> db)
        {
#warning TODO: parse phone into its parts
            dest.RufnummerGesamt = source.Mobile;

            var p = QueryPhoneCorrection(source.Mobile, db);

            dest.Landkennzeichen = p.Telefonlandkennzeichen;
            dest.Vorwahl = p.Telefonvorwahl;
            dest.Rufnummer = p.Telefonrufnummer;
        }

        private void CopyPartnerToPersonTelefonFax(resPartner source, dboPersonTelefon dest, DataService<dboPersonTelefon> db)
        {
#warning TODO: parse phone into its parts
            dest.RufnummerGesamt = source.Fax;

            var p = QueryPhoneCorrection(source.Fax, db);

            dest.Landkennzeichen = p.Telefonlandkennzeichen;
            dest.Vorwahl = p.Telefonvorwahl;
            dest.Rufnummer = p.Telefonrufnummer;
        }

        private const string GültigMonatArray = "111111111111";
        private dboPerson InitDboPerson()
        {
            dboPerson result = null;

            using (var typenSvc = MdbService.GetDataService<dboTypen>())
            {  
                var defaultLandID = (int)typenSvc.Read(new { TypenID = 200525 }).FirstOrDefault().Formularwert;
                LogMilliseconds($"{nameof(InitDboPerson)} get Type<200525>", typenSvc.LastQueryExecutionTimeMS);

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
                LogMilliseconds($"{nameof(InitDboPersonAdresse)} get Type<200120>", typenSvc.LastQueryExecutionTimeMS);

                var defaultLandID = (int)typenSvc.Read(new { TypenID = 200525 }).FirstOrDefault().Formularwert;
                LogMilliseconds($"{nameof(InitDboPersonAdresse)} get Type<200525>", typenSvc.LastQueryExecutionTimeMS);

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
                LogMilliseconds($"{nameof(InitDboPersonEmail)} get Type<200122>", typenSvc.LastQueryExecutionTimeMS);

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

        private dboPersonGruppe InitSystemDeactivateBPK()
        {
            var result = new dboPersonGruppe()
            {
                zGruppeDetailID = 128782,
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
            return (!string.IsNullOrEmpty(partner.Street)
                || !string.IsNullOrEmpty(partner.StreetNumber)
                || !string.IsNullOrEmpty(partner.Zip)
                || !string.IsNullOrEmpty(partner.City));
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
