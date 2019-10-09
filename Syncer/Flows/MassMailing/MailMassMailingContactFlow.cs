using DaDi.Odoo;
using DaDi.Odoo.Models.MassMailing;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.MassMailing
{
    [StudioModel(Name = "fson.mail_mass_mailing_contact")]
    [OnlineModel(Name = "mail.mass_mailing.contact")]
    public class MailMassMailingContactFlow
        : ReplicateSyncFlow
    {
        public MailMassMailingContactFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService) : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonmail_mass_mailing_contact>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<fsonmail_mass_mailing_contact>())
            {
                var studioModel = db.Read(new { mail_mass_mailing_contactID = studioID }).SingleOrDefault();

                // Mandatory child list
                RequestChildJob(SosyncSystem.FundraisingStudio, "fson.mail_mass_mailing_list", studioModel.mail_mass_mailing_listID);

                // Optional child person
                if (studioModel.PersonID.HasValue && studioModel.PersonID > 0)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID.Value);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "partner_id", "list_id" });
            var partnerID = OdooConvert.ToInt32ForeignKey(odooModel["partner_id"], allowNull: true);
            var listID = OdooConvert.ToInt32ForeignKey(odooModel["list_id"], allowNull: false);

            // Mandatory child list
            RequestChildJob(SosyncSystem.FSOnline, "mail.mass_mailing.list", listID.Value);

            // Optional child partner
            if (partnerID.HasValue && partnerID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsonmail_mass_mailing_contact, mailMassMailingContact>(
                studioID,
                action,
                x => x.mail_mass_mailing_contactID,
                (studio, online) =>
                {
                    int? partnerID = null;

                    if (studio.PersonID.HasValue)
                    {
                        partnerID = GetOnlineID<dboPerson>(
                            "dbo.Person",
                            "res.partner",
                            studio.PersonID.Value);
                    }

                    var listID = GetOnlineID<fsonmail_mass_mailing_list>(
                        "fson.mail_mass_mailing_list",
                        "mail.mass_mailing.list",
                        studio.mail_mass_mailing_listID);

                    var countryID = GetCountryIdForLandId(studio.LandID);

                    online.Add("partner_id", partnerID);
                    online.Add("list_id", listID);
                    online.Add("firstname", studio.firstname);
                    online.Add("lastname", studio.lastname);
                    online.Add("phone", studio.phone);
                    online.Add("mobile", studio.mobile);
                    online.Add("street", studio.street);
                    online.Add("street2", studio.street2);
                    online.Add("zip", studio.zip);
                    online.Add("city", studio.city);
                    online.Add("state_id", studio.state_id);
                    online.Add("country_id", countryID);

                    online.Add("bestaetigt_am_um", studio.BestaetigtAmUm);
                    online.Add("bestaetigt_herkunft", studio.BestaetigungsHerkunft);
                    online.Add("bestaetigt_typ", MdbService.GetTypeValue(studio.BestaetigungsTypID));

                    online.Add("pf_mandatsid", studio.pf_mandatsid);
                    online.Add("pf_zahlungsreferenz", studio.pf_zahlungsreferenz);
                    online.Add("pf_bpknachname", studio.pf_bpknachname);
                    online.Add("pf_formularnummer", studio.pf_formularnummer);
                    online.Add("pf_email", studio.pf_email);
                    online.Add("pf_xguid", studio.pf_xguid);
                    online.Add("pf_patenkindvorname", studio.pf_patenkindvorname);
                    online.Add("pf_naechstevorlageam", studio.pf_naechstevorlageam);
                    online.Add("pf_name", studio.pf_name);
                    online.Add("pf_anredelower", studio.pf_anredelower);
                    online.Add("pf_jahr", studio.pf_jahr);
                    online.Add("pf_bank", studio.pf_bank);
                    online.Add("pf_wunschspendenbetrag", studio.pf_wunschspendenbetrag);
                    online.Add("pf_iban", studio.pf_iban);
                    online.Add("pf_titelnachname", studio.pf_titelnachname);
                    online.Add("pf_anredekurz", studio.pf_anredekurz);
                    online.Add("pf_personid", studio.pf_personid);
                    online.Add("pf_nameschenker", studio.pf_nameschenker);
                    online.Add("pf_iban_verschluesselt", studio.pf_iban_verschluesselt);
                    online.Add("pf_patentier", studio.pf_patentier);
                    online.Add("pf_patenkind", studio.pf_patenkind);
                    online.Add("pf_emaildatum", studio.pf_emaildatum);
                    online.Add("pf_anredelang", studio.pf_anredelang);
                    online.Add("pf_vorname", studio.pf_vorname);
                    online.Add("pf_bpkvorname", studio.pf_bpkvorname);
                    online.Add("pf_namebeschenkter", studio.pf_namebeschenkter);
                    online.Add("pf_jahresbeitrag", studio.pf_jahresbeitrag);
                    online.Add("pf_bpkgeburtsdatum", studio.pf_bpkgeburtsdatum);
                    online.Add("pf_bic", studio.pf_bic);
                    online.Add("pf_teilbeitrag", studio.pf_teilbeitrag);
                    online.Add("pf_naechstevorlageammonatjahr", studio.pf_naechstevorlageammonatjahr);
                    online.Add("pf_bpkplz", studio.pf_bpkplz);
                    online.Add("pf_geburtsdatum", studio.pf_geburtsdatum);
                    online.Add("pf_betragspendenquittung", studio.pf_betragspendenquittung);
                    online.Add("pf_zahlungsintervall", studio.pf_zahlungsintervall);
                    online.Add("pf_anrede", studio.pf_anrede);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<mailMassMailingContact, fsonmail_mass_mailing_contact>(
                onlineID,
                action,
                x => x.mail_mass_mailing_contactID,
                (online, studio) =>
                {
                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.partner_id,
                        false);

                    var listID = GetStudioIDFromOnlineReference(
                        "fson.mail_mass_mailing_list",
                        online,
                        x => x.list_id,
                        false);

                    var landID = GetLandIdForCountryId(
                        OdooConvert.ToInt32ForeignKey(online.country_id));

                    studio.PersonID = personID;
                    studio.mail_mass_mailing_listID = listID.Value;
                    studio.firstname = online.firstname;
                    studio.lastname = online.lastname;
                    studio.phone = online.phone;
                    studio.mobile = online.mobile;
                    studio.street = online.street;
                    studio.street2 = online.street2;
                    studio.zip = online.zip;
                    studio.city = online.city;
                    studio.state_id = OdooConvert.ToInt32ForeignKey(online.state_id, true);
                    studio.LandID = landID;

                    studio.BestaetigtAmUm = online.bestaetigt_am_um;
                    studio.BestaetigungsHerkunft = online.bestaetigt_herkunft;
                    studio.BestaetigungsTypID = MdbService.GetTypeID("fsonmail_mass_mailing_contact_BestaetigungsTypID", online.bestaetigt_typ);

                    studio.pf_mandatsid = online.pf_mandatsid;
                    studio.pf_zahlungsreferenz = online.pf_zahlungsreferenz;
                    studio.pf_bpknachname = online.pf_bpknachname;
                    studio.pf_formularnummer = online.pf_formularnummer;
                    studio.pf_email = online.pf_email;
                    studio.pf_xguid = online.pf_xguid;
                    studio.pf_patenkindvorname = online.pf_patenkindvorname;
                    studio.pf_naechstevorlageam = online.pf_naechstevorlageam;
                    studio.pf_name = online.pf_name;
                    studio.pf_anredelower = online.pf_anredelower;
                    studio.pf_jahr = online.pf_jahr;
                    studio.pf_bank = online.pf_bank;
                    studio.pf_wunschspendenbetrag = online.pf_wunschspendenbetrag;
                    studio.pf_iban = online.pf_iban;
                    studio.pf_titelnachname = online.pf_titelnachname;
                    studio.pf_anredekurz = online.pf_anredekurz;
                    studio.pf_personid = online.pf_personid;
                    studio.pf_nameschenker = online.pf_nameschenker;
                    studio.pf_iban_verschluesselt = online.pf_iban_verschluesselt;
                    studio.pf_patentier = online.pf_patentier;
                    studio.pf_patenkind = online.pf_patenkind;
                    studio.pf_emaildatum = online.pf_emaildatum;
                    studio.pf_anredelang = online.pf_anredelang;
                    studio.pf_vorname = online.pf_vorname;
                    studio.pf_bpkvorname = online.pf_bpkvorname;
                    studio.pf_namebeschenkter = online.pf_namebeschenkter;
                    studio.pf_jahresbeitrag = online.pf_jahresbeitrag;
                    studio.pf_bpkgeburtsdatum = online.pf_bpkgeburtsdatum;
                    studio.pf_bic = online.pf_bic;
                    studio.pf_teilbeitrag = online.pf_teilbeitrag;
                    studio.pf_naechstevorlageammonatjahr = online.pf_naechstevorlageammonatjahr;
                    studio.pf_bpkplz = online.pf_bpkplz;
                    studio.pf_geburtsdatum = online.pf_geburtsdatum;
                    studio.pf_betragspendenquittung = online.pf_betragspendenquittung;
                    studio.pf_zahlungsintervall = online.pf_zahlungsintervall;
                    studio.pf_anrede = online.pf_anrede;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
