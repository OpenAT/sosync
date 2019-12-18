using DaDi.Odoo;
using DaDi.Odoo.Extensions;
using DaDi.Odoo.Models;
using dadi_data;
using dadi_data.Interfaces;
using dadi_data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.Person")]
    [OnlineModel(Name = "res.partner")]
    [StudioMultiModel]
    public class PartnerFlow : ReplicateSyncFlow
    {
        #region Constants
        private const string GültigMonatArray = "111111111111";
        #endregion

        #region Constructors
        public PartnerFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }
        #endregion

        #region Methods
        private DateTime? GetPersonWriteDate(dboPersonStack person)
        {
            var query = new DateTime?[]
            {
                person.person != null ? person.person.write_date : (DateTime?)null,
                person.address != null ? person.address.write_date : (DateTime?)null,
                person.phone != null ? person.phone.write_date : (DateTime?)null,
                person.mobile != null ? person.mobile.write_date : (DateTime?)null,
                person.fax != null ? person.fax.write_date : (DateTime?)null,
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
                person.phone != null ? person.phone.sosync_write_date : (DateTime?)null,
                person.mobile != null ? person.mobile.sosync_write_date : (DateTime?)null,
                person.fax != null ? person.fax.sosync_write_date : (DateTime?)null,
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
            using (var addressBlockSvc = MdbService.GetDataService<dboPersonAdresseBlock>())
            using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
            {
                result.person = personSvc.Read(new { PersonID = PersonID }).FirstOrDefault();

                if (result.person == null)
                    throw new SyncerException($"{StudioModelName} {PersonID} did not exist");

                result.address = (from iterAddress in addressSvc.Read(new { PersonID = PersonID })
                                  where iterAddress.GültigVon <= DateTime.Today &&
                                      iterAddress.GültigBis >= DateTime.Today
                                  orderby string.IsNullOrEmpty(iterAddress.GültigMonatArray) ? GültigMonatArray : iterAddress.GültigMonatArray descending,
                                  iterAddress.PersonAdresseID descending
                                  select iterAddress).FirstOrDefault();
              
                if (result.address != null)
                    result.addressAM = addressAMSvc.Read(new { PersonAdresseID = result.address.PersonAdresseID }).FirstOrDefault();

                if (result.address != null)
                    result.addressBlock = addressBlockSvc.Read(new { PersonAdresseID = result.address.PersonAdresseID }).FirstOrDefault();

                result.phone = (from iterPhone in phoneSvc.Read(new { PersonID = PersonID })
                                where iterPhone.GültigVon <= DateTime.Today &&
                                    iterPhone.GültigBis >= DateTime.Today &&
                                    iterPhone.TelefontypID == 400
                                orderby string.IsNullOrEmpty(iterPhone.GültigMonatArray) ? GültigMonatArray : iterPhone.GültigMonatArray descending,
                                iterPhone.PersonTelefonID descending
                                select iterPhone).FirstOrDefault();

                result.mobile = (from itermobile in phoneSvc.Read(new { PersonID = PersonID })
                                where itermobile.GültigVon <= DateTime.Today &&
                                    itermobile.GültigBis >= DateTime.Today &&
                                    itermobile.TelefontypID == 401
                                orderby string.IsNullOrEmpty(itermobile.GültigMonatArray) ? GültigMonatArray : itermobile.GültigMonatArray descending,
                                itermobile.PersonTelefonID descending
                                 select itermobile).FirstOrDefault();

                result.fax = (from iterfax in phoneSvc.Read(new { PersonID = PersonID })
                                where iterfax.GültigVon <= DateTime.Today &&
                                    iterfax.GültigBis >= DateTime.Today &&
                                    iterfax.TelefontypID == 403
                                orderby string.IsNullOrEmpty(iterfax.GültigMonatArray) ? GültigMonatArray : iterfax.GültigMonatArray descending,
                                iterfax.PersonTelefonID descending
                              select iterfax).FirstOrDefault();
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
            DataService<dboPersonTelefon> phoneSvc,
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

            s.Stop();
            LogMs(1, $"{nameof(SetdboPersonStack_fso_ids)} done", Job.ID, Convert.ToInt64(s.Elapsed.TotalMilliseconds));
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
                    { "bpk_forced_street", person.person.BPKErzwungenStrasse },
                    { "sosync_write_date", sosync_write_date }
                    // Do NOT sync bpk_state back!
                };

            if (new int[] { 290, 291 }.Contains(person.person.GeschlechttypID))
                data.Add("gender", person.person.GeschlechttypID == 290 ? "male" : "female");
            else
                data.Add("gender", "other");

            if (person.address != null)
            {
                data.Add("street", person.addressBlock.Strasse);
                data.Add("street_number_web", person.addressBlock.Hausnr);
                data.Add("zip", person.addressBlock.Plz);
                data.Add("city", person.addressBlock.Ort);
                data.Add("anrede_individuell", person.address.AnredeformtypID == 324 ? person.address.IndividuelleAnrede : null);
                data.Add("country_id", GetCountryIdForLandId(person.address.LandID));
            }
            else
            {
                data.Add("street", null);
                data.Add("street_number_web", null);
                data.Add("zip", null);
                data.Add("city", null);
                data.Add("anrede_individuell", null);
            }

            SetDictionaryEntryForObject(data, person.phone, "phone", () => CombinePhone(person.phone), () => null);
            SetDictionaryEntryForObject(data, person.mobile, "mobile", () => CombinePhone(person.mobile), () => null);
            SetDictionaryEntryForObject(data, person.fax, "fax", () => CombinePhone(person.fax), () => null);

            UpdateSyncSourceData(Serializer.ToXML(person));
            
            if (action == TransformType.CreateNew)
            {
                data.Add("sosync_fs_id", person.person.PersonID);

                int odooPartnerId = 0;
                try
                {
                    odooPartnerId = OdooService.Client.CreateModel("res.partner", data);
                }
                finally
                {
                    UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
                    UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooPartnerId);
                }

                // Update the remote id in studio
                using (var personSvc = MdbService.GetDataService<dboPerson>())
                using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
                using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
                {
                    SetdboPersonStack_fso_ids(
                        person, 
                        odooPartnerId, 
                        personSvc, 
                        addressSvc, 
                        phoneSvc, 
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

                    OdooService.Client.UpdateModel("res.partner", data, onlineID, false);

                    // Update the remote id in studio
                    using (var personSvc = MdbService.GetDataService<dboPerson>())
                    using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>())
                    using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>())
                    {
                        SetdboPersonStack_fso_ids(
                            person,
                            onlineID,
                            personSvc,
                            addressSvc,
                            phoneSvc,
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

        private void SetDictionaryEntryForObject<T>(Dictionary<string, object> dict, object item, string key, Func<T> getTrueValue, Func<T> getFalseValue)
        {
            if (item != null)
                dict.Add(key, getTrueValue());
            else
                dict.Add(key, getFalseValue());
        }

        //private void SetDictionaryEntryForGroup<T>(Dictionary<string, object> dict, IStudioGroup group, string key, T trueValue, T falseValue)
        //{
        //    if (group != null)
        //    {
        //        if (group.GültigVon.Date <= DateTime.Today
        //            && group.GültigBis.Date >= DateTime.Today)
        //            dict.Add(key, trueValue);
        //        else
        //            dict.Add(key, falseValue);
        //    }
        //    else
        //    {
        //        dict.Add(key, falseValue);
        //    }
        //}

        private string CombinePhone(dboPersonTelefon phone)
        {
            if (phone == null)
                return null;

            var country = (phone.Landkennzeichen ?? "").Trim();
            var area = (phone.Vorwahl ?? "").Trim();

            if (area.StartsWith("0"))
                area = area.Substring(1);

            return $"{country} {area} {phone.Rufnummer}".Trim();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var partner = OdooService.Client.GetModel<resPartner>("res.partner", onlineID);
            LogMilliseconds($"{nameof(TransformToStudio)} read res.partner", OdooService.Client.LastRpcTime);           

            var sosync_write_date = (partner.Sosync_Write_Date ?? partner.Write_Date).Value;

            if (!IsValidFsID(partner.Sosync_FS_ID))
                partner.Sosync_FS_ID = GetStudioIDFromMssqlViaOnlineID("dbo.Person", "PersonID", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            dboPersonStack person = null;

            using (var personSvc = MdbService.GetDataService<dboPerson>())
            {
                var transaction = personSvc.BeginTransaction();
                var con = personSvc.Connection;

                using (var addressSvc = MdbService.GetDataService<dboPersonAdresse>(con, transaction))
                using (var addressAMSvc = MdbService.GetDataService<dboPersonAdresseAM>(con, transaction))
                using (var phoneSvc = MdbService.GetDataService<dboPersonTelefon>(con, transaction))
                {
                    var addressChanged = false;

                    // Load online model, save it to studio
                    if (action == TransformType.CreateNew)
                    {
                        person = new dboPersonStack();

                        //create new dboPerson
                        person.person = InitDboPerson();
                        SetSyncFields(person.person, onlineID, sosync_write_date);

                        CopyPartnerToPerson(partner, person.person);

                        //create new dboPersonAddress if any associated field has a value
                        if (partner.HasAddress())
                        {
                            person.address = InitDboPersonAdresse();
                            SetSyncFields(person.address, onlineID, sosync_write_date);

                            addressChanged = CopyPartnerToPersonAddress(partner, person.address, person.addressAM, person.addressBlock);

                            person.addressAM = InitDboPersonAdresseAM();
                        }

                        //create new dboPersonTelefon if phone is filled
                        if (partner.HasPhone())
                        {
                            person.phone = InitDboPersonTelefon(dboPersonTelefontyp.phone);
                            SetSyncFields(person.phone, onlineID, sosync_write_date);

                            CopyPartnerPhoneToPersonTelefon(partner.Phone, person.phone, phoneSvc);
                        }

                        //create new dboPersonTelefon if mobile is filled
                        if (partner.HasMobile())
                        {
                            person.mobile = InitDboPersonTelefon(dboPersonTelefontyp.mobile);
                            SetSyncFields(person.mobile, onlineID, sosync_write_date);

                            CopyPartnerPhoneToPersonTelefon(partner.Mobile, person.mobile, phoneSvc);
                        }

                        //create new dboPersonTelefon if phone is filled
                        if (partner.HasFax())
                        {
                            person.fax = InitDboPersonTelefon(dboPersonTelefontyp.fax);
                            SetSyncFields(person.fax, onlineID, sosync_write_date);

                            CopyPartnerPhoneToPersonTelefon(partner.Fax, person.fax, phoneSvc);
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

                            UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, PersonID);
                        }
                        catch (Exception ex)
                        {
                            var debug = $"------------------------------------------\n{personSvc.LastQuery}\n"
                                + $"------------------------------------------\n{addressSvc.LastQuery}\n"
                                + $"------------------------------------------\n{addressAMSvc.LastQuery}\n"
                                + $"------------------------------------------\n{phoneSvc.LastQuery}\n";

                            UpdateSyncTargetAnswer(ex.ToString() + $"\n{debug}", PersonID);
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
                        person = GetCurrentdboPersonStack(PersonID);

                        UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(person));

                        CopyPartnerToPerson(partner, person.person);

                        SetSyncFields(person.person, onlineID, sosync_write_date);

                        if (person.address == null)
                        {
                            if (partner.HasAddress())
                            {
                                person.address = InitDboPersonAdresse();
                                person.addressAM = InitDboPersonAdresseAM();

                                person.address.PersonID = PersonID;
                                person.addressAM.PersonID = PersonID;

                                addressChanged = CopyPartnerToPersonAddress(partner, person.address, person.addressAM, person.addressBlock);

                                SetSyncFields(person.address, onlineID, sosync_write_date);
                            }
                        }
                        else
                        {
                            addressChanged = CopyPartnerToPersonAddress(partner, person.address, person.addressAM, person.addressBlock);
                            SetSyncFields(person.address, onlineID, sosync_write_date);
                        }

                        if (person.phone == null)
                        {
                            if (partner.HasPhone())
                            {
                                person.phone = InitDboPersonTelefon(dboPersonTelefontyp.phone);
                                person.phone.PersonID = PersonID;

                                CopyPartnerPhoneToPersonTelefon(partner.Phone, person.phone, phoneSvc);
                                SetSyncFields(person.phone, onlineID, sosync_write_date);
                            }
                        }
                        else
                        {
                            CopyPartnerPhoneToPersonTelefon(partner.Phone, person.phone, phoneSvc);
                            SetSyncFields(person.phone, onlineID, sosync_write_date);
                        }

                        if (person.mobile == null)
                        {
                            if (partner.HasMobile())
                            {
                                person.mobile = InitDboPersonTelefon(dboPersonTelefontyp.mobile);
                                person.mobile.PersonID = PersonID;

                                CopyPartnerPhoneToPersonTelefon(partner.Mobile, person.mobile, phoneSvc);
                                SetSyncFields(person.mobile, onlineID, sosync_write_date);
                            }
                        }
                        else
                        {
                            CopyPartnerPhoneToPersonTelefon(partner.Mobile, person.mobile, phoneSvc);
                            SetSyncFields(person.mobile, onlineID, sosync_write_date);
                        }

                        if (person.fax == null)
                        {
                            if (partner.HasFax())
                            {
                                person.fax = InitDboPersonTelefon(dboPersonTelefontyp.fax);
                                person.fax.PersonID = PersonID;

                                CopyPartnerPhoneToPersonTelefon(partner.Fax, person.fax, phoneSvc);
                                SetSyncFields(person.fax, onlineID, sosync_write_date);
                            }
                        }
                        else
                        {
                            CopyPartnerPhoneToPersonTelefon(partner.Fax, person.fax, phoneSvc);
                            SetSyncFields(person.fax, onlineID, sosync_write_date);
                        }

                        UpdateSyncTargetRequest(Serializer.ToXML(person));

                        try
                        {
                            if (addressChanged)
                                CreateOrUpdateAddress(addressSvc, addressAMSvc, person.address, person.addressAM);

                            CreateOrUpdate(phoneSvc, person.phone, person.phone != null ? person.phone.PersonTelefonID : 0, "(Festnetz)");
                            CreateOrUpdate(phoneSvc, person.mobile, person.mobile != null ? person.mobile.PersonTelefonID : 0, "(Mobil)");
                            CreateOrUpdate(phoneSvc, person.fax, person.fax != null ? person.fax.PersonTelefonID : 0, "(Fax)");

                            personSvc.Update(person.person);
                            LogMilliseconds($"{nameof(TransformToStudio)} update dbo.Person", personSvc.LastQueryExecutionTimeMS);

                            SetdboPersonStack_fso_ids(
                               person,
                               onlineID,
                               personSvc,
                               addressSvc,
                               phoneSvc,
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

                    // Start PAC procedure, for new or changed addresses
                    if (addressChanged)
                    {
                        var xmlString = $"<IDS><PersonAdresse PersonAdresseID=\"{person.address.PersonAdresseID}\" Arbeit=\"PACSuche\" /></IDS>";
                        personSvc.ExecuteNonQueryProcedure("stp_800_AsyncSend_0020_PersonPAC", new { Person = xmlString });
                    }
                    // --

                    personSvc.CommitTransaction();
                    LogMilliseconds($"{nameof(TransformToStudio)} commit transaction dbo.Person", personSvc.LastQueryExecutionTimeMS);

                    if (person != null && person.person != null)
                    {
                        var s = new Stopwatch();
                        s.Start();

                        var p = new DynamicParameters();
                        p.Add("PersonID", person.person.PersonID, System.Data.DbType.Int32);
                        personSvc.ExecuteProcedure("[dbo].[stp_300_DBUpdate_0010_PersonAdresseblock]", p);

                        s.Stop();
                        LogMilliseconds($"{nameof(TransformToStudio)} executing [dbo].[stp_300_DBUpdate_0010_PersonAdresseblock]", s.Elapsed.TotalMilliseconds);
                    }
                }
            }
        }

        private void SetSyncFields(ISosyncable model, int? onlineID, DateTime? sosync_write_date)
        {
            model.sosync_fso_id = onlineID;
            model.sosync_write_date = sosync_write_date;
            model.noSyncJobSwitch = true;
        }

        private void CreateOrUpdateAddress(
            DataService<dboPersonAdresse> addressSvc,
            DataService<dboPersonAdresseAM> addressAMSvc,
            dboPersonAdresse address,
            dboPersonAdresseAM addressAM)
        {
            if (address != null)
            {
                if (address.PersonAdresseID == 0)
                {
                    addressSvc.Create(address);
                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonAdresse", addressSvc.LastQueryExecutionTimeMS);

                    addressAM.PersonAdresseID = address.PersonAdresseID;

                    addressAMSvc.Create(addressAM);
                    LogMilliseconds($"{nameof(TransformToStudio)} create dbo.PersonAdresseAM", addressAMSvc.LastQueryExecutionTimeMS);
                }
                else
                {
                    addressSvc.Update(address);
                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonAdresse", addressSvc.LastQueryExecutionTimeMS);

                    addressAMSvc.Update(addressAM);
                    LogMilliseconds($"{nameof(TransformToStudio)} update dbo.PersonAdresseAM", addressSvc.LastQueryExecutionTimeMS);
                }
            }
        }

        private void CreateOrUpdate<TService, TModel>(TService svc, TModel model, int modelID, string customLog = "")
            where TModel : MdbModelBase, new()
            where TService : DataService<TModel>
        {
            if (model != null)
            {
                if (modelID == 0)
                {
                    svc.Create(model);
                    LogMilliseconds($"{nameof(TransformToStudio)} create {typeof(TModel).Name} {customLog}", svc.LastQueryExecutionTimeMS);
                }
                else
                {
                    svc.Update(model);
                    LogMilliseconds($"{nameof(TransformToStudio)} update {typeof(TModel).Name} {customLog}", svc.LastQueryExecutionTimeMS);
                }
            }
        }

        private void SetGroupValidity(IStudioGroup group, bool? addGroup)
        {
            if (addGroup ?? false == true)
            {
                if (group.GültigVon > DateTime.Today)
                    group.GültigVon = DateTime.Today;

                group.GültigBis = new DateTime(2099, 12, 31);
            }
            else
            {
                // When disabling, set the previous day, othwerwise the group is
                // still valid for the current day.
                // This is consistent with the FRST user interface.
                if (group.GültigVon > DateTime.Today.AddDays(-1))
                    group.GültigVon = DateTime.Today.AddDays(-1);

                group.GültigBis = DateTime.Today.AddDays(-1);
            }
        }

        private TGroup SetupGroup<TGroup>(
            TGroup group,
            bool addGroup,
            int? onlineID,
            int zGruppeDetailID,
            DateTime? sosync_write_date,
            Action<TGroup> identityInitializer)
            where TGroup : MdbModelBase, IStudioGroup, ISosyncable, new()
        {
            if (group == null)
            {
                if (addGroup)
                {
                    group = InitGroup<TGroup>(zGruppeDetailID);
                    identityInitializer?.Invoke(group);
                    SetSyncFields(group, onlineID, sosync_write_date);
                }
            }
            else
            {
                SetGroupValidity(group, addGroup);
                SetSyncFields(group, onlineID, sosync_write_date);
            }

            return group;
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
            dest.BPKErzwungenStrasse = source.BPKForcedStreet;
            dest.fso_bpk_state = source.BPKState;

            if (new string[] { "male", "female" }.Contains(source.Gender))
                dest.GeschlechttypID = source.Gender == "male" ? 290 : 291;
            else if (source.Gender == "other" && !new int[] { 292, 293, 294, 295, 0 }.Contains(dest.GeschlechttypID))
                dest.GeschlechttypID = 2005821;
            else if (string.IsNullOrEmpty(source.Gender))
                dest.GeschlechttypID = 0;

        }

        private bool IsAddressFieldEqual(string src, string dst)
        {
            return (src ?? "").ToLower().Trim() == (dst ?? "").ToLower().Trim();
        }

        private bool AddressChanged(resPartner source, dboPersonAdresseBlock destBlock)
        {
            // If no address block is present, use a new empty one
            // for comparison
            if (destBlock == null)
                destBlock = new dboPersonAdresseBlock();

            var streetEqual = IsAddressFieldEqual(source.Street, destBlock.Strasse);
            var streetNrEqual = IsAddressFieldEqual(source.StreetNumber, destBlock.Hausnr);
            var zipEqual = IsAddressFieldEqual(source.Zip, destBlock.Plz);
            var cityEqual = IsAddressFieldEqual(source.City, destBlock.Ort);

            return !(streetEqual && streetNrEqual && zipEqual && cityEqual);
        }

        private bool CopyPartnerToPersonAddress(resPartner source, dboPersonAdresse dest, dboPersonAdresseAM destAM, dboPersonAdresseBlock destBlock)
        {
            if (!AddressChanged(source, destBlock))
                return false;

            dest.LandID = GetLandIdForCountryId(
                OdooConvert.ToInt32ForeignKey(source.CountryID, true))
                ?? 0;

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

            if (destAM != null)
                destAM.PAC = 0;

            return true;
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

        private void CopyPartnerPhoneToPersonTelefon(string fullNumber, dboPersonTelefon dest, DataService<dboPersonTelefon> db)
        {
            dest.RufnummerGesamt = fullNumber;

            var p = QueryPhoneCorrection(fullNumber, db);

            dest.Landkennzeichen = p.Telefonlandkennzeichen;
            dest.Vorwahl = p.Telefonvorwahl;
            dest.Rufnummer = p.Telefonrufnummer;
        }

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

        private TGroup InitGroup<TGroup>(int zGruppeDetailID) where TGroup : IStudioGroup, new()
        {
            TGroup result = new TGroup();

            result.zGruppeDetailID = zGruppeDetailID;
            result.Steuerung = true;
            result.GültigVon = DateTime.Today;
            result.GültigBis = new DateTime(2099, 12, 31);
            result.xIDA = 0;

            return result;
        }
        #endregion
    }
}
